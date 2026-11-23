using GreenhouseSimulator.Core.Models;
using GreenhouseSimulator.Core.Models.Actuators;
using GreenhouseSimulator.Core.Models.Biology;
using GreenhouseSimulator.Core.Models.Plans;
using GreenhouseSimulator.Core.Models.Sensors;
using GreenhouseSimulator.Core.Models.Statistics;
using GreenhouseSimulator.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GreenhouseSimulator.Core.Controllers
{
    public enum SimulationResult
    {
        None,
        Completed,
        Failed,
        Stopped
    }

    public class GreenhouseController
    {
        private readonly List<string> _conflictWarnings = new();
        public IReadOnlyList<string> ConflictWarnings => _conflictWarnings;
        public event Action<string, double>? AlertTriggered;
        private readonly Greenhouse _greenhouse;
        private readonly IPlanRepository _repository;
        private readonly CropGrowthModel _cropGrowth;
        private Dictionary<Actuator, PidController> _regulators = new();
        private readonly Dictionary<Actuator, PidController> _acidRegulators = new();
        private readonly HashSet<ActiveSensor> _boundSensors = new();
        private GrowingPlan? _activePlan;
        private bool _isRunning;
        private bool _isPaused;
        private double? _previousTemperatureTarget;

        public Greenhouse Greenhouse => _greenhouse;
        public CropGrowthModel CropGrowth => _cropGrowth;
        public GrowingPlan? ActivePlan => _activePlan;
        public SimulationStatistics Statistics { get; } = new();
        public DateTime? StartDate { get; private set; }
        public int ElapsedHours { get; private set; }
        public int CurrentDay => ElapsedHours / 24 + 1;
        public int HourOfDay => ElapsedHours % 24;
        public bool IsRunning => _isRunning;
        public bool IsPaused => _isPaused;
        public SimulationResult LastResult { get; private set; }
        public string? FailureMessage { get; private set; }

        public GreenhouseController(Greenhouse greenhouse, IPlanRepository repository)
        {
            _greenhouse = greenhouse ?? throw new ArgumentNullException(nameof(greenhouse));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _cropGrowth = new CropGrowthModel(greenhouse.Config, greenhouse.Grid.WidthCells, greenhouse.Grid.LengthCells);
            _activePlan = null;
            ElapsedHours = 0;
            _isRunning = false;
            _isPaused = false;

            SubscribeToActiveSensors();
        }

        public IReadOnlyList<string> ListPlans() => _repository.ListPlans();

        public GrowingPlan LoadPlan(string cropName) => _repository.LoadPlan(cropName);

        public void AddPlan(GrowingPlan plan)
        {
            ArgumentNullException.ThrowIfNull(plan);
            plan.EnsureValid();

            if (_repository.Contains(plan.CropName))
            {
                throw new InvalidOperationException($"A growing plan for '{plan.CropName}' already exists.");
            }

            _repository.SavePlan(plan);
        }

        public void UpdatePlan(GrowingPlan plan)
        {
            ArgumentNullException.ThrowIfNull(plan);
            plan.EnsureValid();

            if (!_repository.Contains(plan.CropName))
            {
                throw new InvalidOperationException($"A growing plan for '{plan.CropName}' does not exist.");
            }

            _repository.SavePlan(plan);

            if (_activePlan != null
                && string.Equals(_activePlan.CropName, plan.CropName, StringComparison.OrdinalIgnoreCase))
            {
                _activePlan = plan;
            }
        }

        public void StartCycle(string planName)
        {
            if (string.IsNullOrWhiteSpace(planName))
            {
                throw new ArgumentException("Plan name cannot be empty.", nameof(planName));
            }

            _conflictWarnings.Clear();
            _greenhouse.ResetCycleState();
            _previousTemperatureTarget = null;
            ResetRegulators();
            Statistics.Reset();

            _activePlan = _repository.LoadPlan(planName);
            _cropGrowth.SetPlannedDuration(_activePlan.TotalDurationDays);
            _cropGrowth.Reset();

            StartDate = DateTime.Now;
            ElapsedHours = 0;
            _isRunning = true;
            _isPaused = false;
            LastResult = SimulationResult.None;
            FailureMessage = null;

            PlanPhase phase = _activePlan.GetPhaseForDay(CurrentDay);
            ApplySensorThresholds(phase);
            RegulateEnvironment(phase);
        }

        public void StopCycle()
        {
            if (_isRunning && LastResult == SimulationResult.None)
            {
                LastResult = SimulationResult.Stopped;
            }

            _isRunning = false;
            _isPaused = false;
            Statistics.FlushOpenDay();

            foreach (var actuator in _greenhouse.GetAllActuators())
            {
                actuator.TurnOff();
            }

            ResetRegulators();
        }

        public SimulationResult TakeResult(out string? failureMessage)
        {
            SimulationResult result = LastResult;
            failureMessage = FailureMessage;
            LastResult = SimulationResult.None;
            FailureMessage = null;
            return result;
        }

        public void Pause()
        {
            if (!_isRunning)
            {
                throw new InvalidOperationException("Cannot pause a cycle that is not running.");
            }

            _isPaused = true;
        }

        public void Resume()
        {
            if (!_isRunning)
            {
                throw new InvalidOperationException("Cannot resume a cycle that is not running.");
            }

            _isPaused = false;
        }

        public void SimulationTick()
        {
            if (!_isRunning || _isPaused || _activePlan == null)
            {
                return; 
            }

            try
            {
                PlanPhase phase = _activePlan.GetPhaseForDay(CurrentDay);
                ApplySensorThresholds(phase);
                _greenhouse.Tick(phase, HourOfDay, () => RegulateEnvironment(phase));
                _cropGrowth.Advance(_greenhouse.Grid, phase, HourOfDay, CurrentDay, _greenhouse.Config.HoursPerTick);
                Statistics.RecordTick(
                    CurrentDay,
                    HourOfDay,
                    _greenhouse.CurrentTemperature,
                    _greenhouse.CurrentHumidity,
                    _greenhouse.CurrentPh,
                    _greenhouse.CurrentLight,
                    phase.GetTempCorridor(HourOfDay),
                    phase.HumidityCorridor,
                    phase.PhCorridor,
                    HourOfDay < phase.LightDurationHours);
                ElapsedHours++;

                if (ElapsedHours >= _activePlan.TotalDurationDays * 24)
                {
                    string? regulationFailure = _greenhouse.DescribeMissingRegulators();
                    if (regulationFailure == null)
                    {
                        LastResult = SimulationResult.Completed;
                    }
                    else
                    {
                        LastResult = SimulationResult.Failed;
                        FailureMessage = regulationFailure;
                    }

                    StopCycle();
                }
            }
            catch (Exception ex)
            {
                LastResult = SimulationResult.Failed;
                FailureMessage = ex.Message;
                StopCycle();
                throw;
            }
        }

        private void AssignZonalPower(Actuator actuator, Func<ActiveSensor, double?> errorOf)
        {
            AssignZonalPower(actuator, errorOf, _regulators, actuator.SetTargetPower);
        }

        private void AssignZonalPower(
            Actuator actuator,
            Func<ActiveSensor, double?> errorOf,
            Dictionary<Actuator, PidController> regulators,
            Action<double> apply)
        {
            double minWeight = _greenhouse.Config.MinActuatorWeight;
            bool useGaussian = _greenhouse.Config.UseGaussianAttenuation;

            ActiveSensor? demandSensor = null;
            double demandError = 0.0;
            double demandWeight = 0.0;
            foreach (ActiveSensor sensor in _greenhouse.GetAllSensors().OfType<ActiveSensor>())
            {
                double weight = actuator.GetAttenuation(sensor.Location, useGaussian);
                if (weight <= minWeight)
                {
                    continue;
                }

                double? error = errorOf(sensor);
                if (error == null)
                {
                    continue;
                }

                if (demandSensor == null || error.Value > demandError)
                {
                    demandSensor = sensor;
                    demandError = error.Value;
                    demandWeight = weight;
                }
            }

            if (demandSensor == null)
            {
                apply(0.0);
                return;
            }

            if (!regulators.TryGetValue(actuator, out PidController? regulator))
            {
                regulator = new PidController(_greenhouse.Config);
                regulators[actuator] = regulator;
            }

            double command = regulator.Compute(demandError, ControlStepHours()) * demandWeight;
            apply(command);
        }

        private double ControlStepHours()
        {
            return _greenhouse.Config.HoursPerTick / Math.Max(1, _greenhouse.Config.SubstepsPerHour);
        }

        private void ResetRegulators()
        {
            foreach (PidController regulator in _regulators.Values)
            { 
                regulator.Reset(); 
            }

            foreach (PidController regulator in _acidRegulators.Values)
            {
                regulator.Reset();
            }
        }

        private double? GetDehumidifyError(ActiveSensor sensor, ParameterCorridor humidity)
        {
            if (sensor.Type != SensorType.Humidity || sensor.CurrentValue <= humidity.MaxValue)
            {
                return null;
            }

            return (sensor.CurrentValue - humidity.TargetValue) / _greenhouse.Config.HumidityPercentPerDegree;
        }

        private double HeatingTarget(Heater heater, ParameterCorridor temperature, ParameterCorridor humidity)
        {
            if (!HumidityExceedsMaximum(heater, humidity))
            {
                return temperature.TargetValue;
            }

            return temperature.MaxValue;
        }

        private bool HumidityExceedsMaximum(Actuator actuator, ParameterCorridor humidity)
        {
            double minWeight = _greenhouse.Config.MinActuatorWeight;
            bool useGaussian = _greenhouse.Config.UseGaussianAttenuation;
            foreach (ActiveSensor sensor in _greenhouse.GetAllSensors().OfType<ActiveSensor>())
            {
                if (sensor.Type != SensorType.Humidity || !sensor.HasMeasurement)
                {
                    continue;
                }

                if (actuator.GetAttenuation(sensor.Location, useGaussian) <= minWeight)
                {
                    continue;
                }

                if (sensor.CurrentValue > humidity.MaxValue)
                {
                    return true;
                }
            }

            return false;
        }

        private double CoolingPowerCap(AirConditioner conditioner, ParameterCorridor temperature)
        {
            double minWeight = _greenhouse.Config.MinActuatorWeight;
            bool useGaussian = _greenhouse.Config.UseGaussianAttenuation;
            double tightest = double.PositiveInfinity;
            double weightAtTightest = 1.0;
            bool found = false;
            foreach (ActiveSensor sensor in _greenhouse.GetAllSensors().OfType<ActiveSensor>())
            {
                if (sensor.Type != SensorType.Temperature || !sensor.HasMeasurement)
                {
                    continue;
                }

                double weight = conditioner.GetAttenuation(sensor.Location, useGaussian);
                if (weight <= minWeight)
                {
                    continue;
                }

                double headroom = sensor.CurrentValue - temperature.TargetValue;
                if (!found || headroom < tightest)
                {
                    found = true;
                    tightest = headroom;
                    weightAtTightest = weight;
                }
            }

            if (!found)
            {
                tightest = _greenhouse.Grid.GetTemperatureAt(conditioner.Location) - temperature.TargetValue;
                weightAtTightest = 1.0;
            }

            if (tightest <= 0.0 || conditioner.MaxPower <= 0.0)
            {
                return 0.0;
            }

            double cap = tightest / (conditioner.MaxPower * Math.Max(weightAtTightest, 0.05) * ControlStepHours());
            return Math.Clamp(cap, 0.0, 1.0);
        }

        private void RecordUnresolvedHumidityConflicts(PlanPhase phase)
        {
            double minWeight = _greenhouse.Config.MinActuatorWeight;
            bool useGaussian = _greenhouse.Config.UseGaussianAttenuation;

            foreach (ActiveSensor sensor in _greenhouse.GetAllSensors().OfType<ActiveSensor>())
            {
                if (sensor.Type != SensorType.Humidity || sensor.CurrentValue <= phase.HumidityCorridor.MaxValue)
                {
                    continue;
                }

                bool hasReachableAc = _greenhouse.GetAllActuators()
                    .OfType<AirConditioner>()
                    .Any(ac => ac.GetAttenuation(sensor.Location, useGaussian) > minWeight);

                if (hasReachableAc)
                {
                    continue;
                }

                string warning = $"День {CurrentDay}, ч. {HourOfDay}: Конфликт в зоне ({sensor.Location.X:F1}; {sensor.Location.Y:F1}) — высокая влажность ({sensor.CurrentValue:F1}%), кондиционер в зоне отсутствует.";
                if (!_conflictWarnings.Contains(warning))
                {
                    _conflictWarnings.Add(warning);
                }
            }
        }

        private void RegulateEnvironment(PlanPhase phase)
        {
            _greenhouse.SampleMissingSensors();

            ParameterCorridor tempCorridor = phase.GetTempCorridor(HourOfDay);
            if (_previousTemperatureTarget != tempCorridor.TargetValue)
            {
                ResetRegulators();
                _previousTemperatureTarget = tempCorridor.TargetValue;
            }

            bool lightsOn = HourOfDay < phase.LightDurationHours;
            RecordUnresolvedHumidityConflicts(phase);

            foreach (var actuator in _greenhouse.GetAllActuators())
            {
                switch (actuator)
                {
                    case Heater heater:
                        double heatTarget = HeatingTarget(heater, tempCorridor, phase.HumidityCorridor);
                        AssignZonalPower(heater, sensor =>
                        {
                            if (sensor.Type != SensorType.Temperature)
                            {
                                return null;
                            }

                            return heatTarget - sensor.CurrentValue;
                        });
                        break;

                    case AirConditioner conditioner:
                        AssignZonalPower(conditioner, sensor =>
                        {
                            if (sensor.Type == SensorType.Temperature)
                            {
                                return sensor.CurrentValue - tempCorridor.TargetValue;
                            }

                            return GetDehumidifyError(sensor, phase.HumidityCorridor);
                        }, _regulators, command =>
                        {
                            if (HumidityExceedsMaximum(conditioner, phase.HumidityCorridor))
                            {
                                conditioner.SetTargetPower(command);
                                return;
                            }

                            conditioner.SetTargetPower(Math.Min(command, CoolingPowerCap(conditioner, tempCorridor)));
                        });
                        break;

                    case Humidifier humidifier:
                        AssignZonalPower(humidifier, sensor =>
                        {
                            if (sensor.Type != SensorType.Humidity)
                            {
                                return null;
                            }

                            return phase.HumidityCorridor.TargetValue - sensor.CurrentValue;
                        });
                        break;

                    case LightingSystem lighting:
                        SetPower(lighting, lightsOn);
                        break;

                    case FertilizerDoser doser:
                        AssignZonalPower(doser, sensor =>
                        {
                            if (sensor.Type != SensorType.Ph)
                            {
                                return null;
                            }

                            return phase.PhCorridor.TargetValue - sensor.CurrentValue;
                        });
                        AssignZonalPower(doser, sensor =>
                        {
                            if (sensor.Type != SensorType.Ph)
                            {
                                return null;
                            }

                            return sensor.CurrentValue - phase.PhCorridor.TargetValue;
                        }, _acidRegulators, doser.SetAcidTarget);
                        break;
                }
            }

            BalanceDehumidifyReheat(tempCorridor, phase.HumidityCorridor);
        }

        private void BalanceDehumidifyReheat(ParameterCorridor temperature, ParameterCorridor humidity)
        {
            double minWeight = _greenhouse.Config.MinActuatorWeight;
            bool useGaussian = _greenhouse.Config.UseGaussianAttenuation;
            var heaterRatios = new Dictionary<Heater, double>();

            foreach (AirConditioner conditioner in _greenhouse.GetAllActuators().OfType<AirConditioner>())
            {
                if (!HumidityExceedsMaximum(conditioner, humidity))
                {
                    continue;
                }

                (double X, double Y) point = ControlPoint(conditioner, out double measuredTemp);
                double acWeight = conditioner.GetAttenuation(point, useGaussian);
                if (acWeight <= 0.0)
                {
                    acWeight = 1.0;
                }

                var heaters = _greenhouse.GetAllActuators()
                    .OfType<Heater>()
                    .Where(heater => heater.GetAttenuation(point, useGaussian) > minWeight)
                    .ToList();

                if (heaters.Count == 0)
                {
                    conditioner.SetTargetPower(Math.Min(conditioner.TargetPowerRatio, CoolingPowerCap(conditioner, temperature)));
                    continue;
                }

                double heaterCapacity = heaters.Sum(heater => heater.MaxPower * heater.GetAttenuation(point, useGaussian));
                double acAtPoint = conditioner.MaxPower * acWeight;
                double approachHours = 0.25;
                double desiredRate = (temperature.MaxValue - measuredTemp) / approachHours;
                double desiredNet = Math.Clamp(desiredRate, -acAtPoint, heaterCapacity);
                double dryCooling = conditioner.TargetPowerRatio * acAtPoint;
                double acCooling = Math.Min(dryCooling, Math.Max(0.0, heaterCapacity - desiredNet));
                if (desiredNet < 0.0)
                {
                    acCooling = Math.Max(acCooling, Math.Min(-desiredNet, acAtPoint));
                }

                double heaterHeating = Math.Clamp(acCooling + desiredNet, 0.0, heaterCapacity);
                conditioner.SetTargetPower(acAtPoint > 0.0 ? acCooling / acAtPoint : 0.0);

                double ratio = heaterCapacity > 0.0 ? heaterHeating / heaterCapacity : 0.0;
                foreach (Heater heater in heaters)
                {
                    heaterRatios[heater] = heaterRatios.TryGetValue(heater, out double already) ? already + ratio : ratio;
                }

                if (_regulators.TryGetValue(conditioner, out PidController? conditionerPid))
                {
                    conditionerPid.Reset();
                }
            }

            foreach (KeyValuePair<Heater, double> pair in heaterRatios)
            {
                pair.Key.SetTargetPower(pair.Value);
                if (_regulators.TryGetValue(pair.Key, out PidController? heaterPid))
                {
                    heaterPid.Reset();
                }
            }
        }

        private (double X, double Y) ControlPoint(AirConditioner conditioner, out double temperature)
        {
            double minWeight = _greenhouse.Config.MinActuatorWeight;
            bool useGaussian = _greenhouse.Config.UseGaussianAttenuation;
            ActiveSensor? closest = null;
            double bestWeight = minWeight;

            foreach (ActiveSensor sensor in _greenhouse.GetAllSensors().OfType<ActiveSensor>())
            {
                if (sensor.Type != SensorType.Temperature || !sensor.HasMeasurement)
                {
                    continue;
                }

                double weight = conditioner.GetAttenuation(sensor.Location, useGaussian);
                if (weight <= bestWeight)
                {
                    continue;
                }

                bestWeight = weight;
                closest = sensor;
            }

            if (closest != null)
            {
                temperature = closest.CurrentValue;
                return closest.Location;
            }

            temperature = _greenhouse.Grid.GetTemperatureAt(conditioner.Location);
            return conditioner.Location;
        }

        private void ApplySensorThresholds(PlanPhase phase)
        {
            foreach (var sensor in _greenhouse.GetAllSensors().OfType<ActiveSensor>())
            {
                ParameterCorridor? corridor = sensor.Type switch
                {
                    SensorType.Temperature => phase.GetTempCorridor(HourOfDay),
                    SensorType.Humidity => phase.HumidityCorridor,
                    SensorType.Ph => phase.PhCorridor,
                    _ => null
                };

                if (corridor == null)
                {
                    continue;
                }

                sensor.SetThresholds(corridor.TargetValue, corridor.GetToleranceBelow(), corridor.GetToleranceAbove());
            }
        }

        private static void SetPower(Actuator actuator, bool on)
        {
            if (on)
            {
                actuator.TurnOn();
            }
            else
            { 
                actuator.TurnOff(); 
            }
        }

        public void BindNewSensors()
        {
            SubscribeToActiveSensors();
        }

        public void SyncRegulation()
        {
            if (!_isRunning || _activePlan == null)
            {
                return;
            }

            PlanPhase phase = _activePlan.GetPhaseForDay(CurrentDay);
            ApplySensorThresholds(phase);
            RegulateEnvironment(phase);
        }

        private void SubscribeToActiveSensors()
        {
            foreach (var sensor in _greenhouse.GetAllSensors().OfType<ActiveSensor>())
            {
                BindSensor(sensor);
            }
        }

        private void BindSensor(ActiveSensor sensor)
        {
            if (!_boundSensors.Add(sensor))
            {
                return;
            }

            sensor.OnThresholdExceeded += (sender, args) =>
            {
                HandleSensorAlert(args.SensorId, args.CurrentValue);
            };
        }

        private void HandleSensorAlert(string sensorId, double value)
        {
            Statistics.RecordAlert();
            AlertTriggered?.Invoke(sensorId, value);
        }
    }
}
