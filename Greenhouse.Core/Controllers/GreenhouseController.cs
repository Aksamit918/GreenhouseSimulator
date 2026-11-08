using GreenhouseSimulator.Core.Models;
using GreenhouseSimulator.Core.Models.Actuators;
using GreenhouseSimulator.Core.Models.Plans;
using GreenhouseSimulator.Core.Models.Sensors;
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
        private Dictionary<Actuator, PidController> _regulators = new();
        private readonly HashSet<ActiveSensor> _boundSensors = new();
        private GrowingPlan? _activePlan;
        private bool _isRunning;
        private bool _isPaused;
        private double? _previousTemperatureTarget;

        public Greenhouse Greenhouse => _greenhouse;
        public GrowingPlan? ActivePlan => _activePlan;
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

            _activePlan = _repository.LoadPlan(planName);

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
                RegulateEnvironment(phase);
                _greenhouse.Tick(phase, HourOfDay);

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
            double minWeight = _greenhouse.Config.MinActuatorWeight;
            bool useGaussian = _greenhouse.Config.UseGaussianAttenuation;

            double? worstError = null;
            foreach (ActiveSensor sensor in _greenhouse.GetAllSensors().OfType<ActiveSensor>())
            {
                if (actuator.GetAttenuation(sensor.Location, useGaussian) <= minWeight)
                {
                    continue;
                }

                double? error = errorOf(sensor);
                if (error == null)
                {
                    continue;
                }

                if (worstError == null || error > worstError)
                {
                    worstError = error;
                }
            }

            if (worstError == null)
            {
                actuator.SetTargetPower(0.0);
                return;
            }

            if (!_regulators.TryGetValue(actuator, out PidController? regulator))
            {
                regulator = new PidController(_greenhouse.Config);
                _regulators[actuator] = regulator;
            }

            actuator.SetTargetPower(regulator.Compute(worstError.Value, _greenhouse.Config.HoursPerTick));
        }

        private void ResetRegulators()
        {
            foreach (PidController regulator in _regulators.Values)
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

        private bool IsNearOverheat(ActiveSensor sensor, ParameterCorridor tempCorridor)
        {
            double localTemp = _greenhouse.Grid.GetTemperatureAt(sensor.Location);
            return localTemp > tempCorridor.MaxValue - _greenhouse.Config.OverheatMargin;
        }

        private void RecordUnresolvedHumidityConflicts(PlanPhase phase, ParameterCorridor tempCorridor)
        {
            double minWeight = _greenhouse.Config.MinActuatorWeight;
            bool useGaussian = _greenhouse.Config.UseGaussianAttenuation;

            foreach (ActiveSensor sensor in _greenhouse.GetAllSensors().OfType<ActiveSensor>())
            {
                if (sensor.Type != SensorType.Humidity || sensor.CurrentValue <= phase.HumidityCorridor.MaxValue)
                {
                    continue;
                }

                if (!IsNearOverheat(sensor, tempCorridor))
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

                double localTemp = _greenhouse.Grid.GetTemperatureAt(sensor.Location);
                string warning = $"День {CurrentDay}, ч. {HourOfDay}: Конфликт в зоне ({sensor.Location.X:F1}; {sensor.Location.Y:F1}) — высокая влажность ({sensor.CurrentValue:F1}%) при риске перегрева ({localTemp:F1}°C), кондиционер в зоне отсутствует.";
                if (!_conflictWarnings.Contains(warning))
                {
                    _conflictWarnings.Add(warning);
                }
            }
        }

        private void RegulateEnvironment(PlanPhase phase)
        {
            ParameterCorridor tempCorridor = phase.GetTempCorridor(HourOfDay);
            if (_previousTemperatureTarget != tempCorridor.TargetValue)
            {
                ResetRegulators();
                _previousTemperatureTarget = tempCorridor.TargetValue;
            }

            bool lightsOn = HourOfDay < phase.LightDurationHours;
            RecordUnresolvedHumidityConflicts(phase, tempCorridor);

            foreach (var actuator in _greenhouse.GetAllActuators())
            {
                switch (actuator)
                {
                    case Heater heater:
                        AssignZonalPower(heater, sensor =>
                        {
                            if (sensor.Type == SensorType.Temperature)
                            {
                                return tempCorridor.TargetValue - sensor.CurrentValue;
                            }
                            double? dehumidifyError = GetDehumidifyError(sensor, phase.HumidityCorridor);
                            if (dehumidifyError == null || IsNearOverheat(sensor, tempCorridor))
                            {
                                return null;
                            }
                            return dehumidifyError;
                        });
                        break;

                    case AirConditioner conditioner:
                        AssignZonalPower(conditioner, sensor =>
                        {
                            if (sensor.Type == SensorType.Temperature)
                            {
                                return sensor.CurrentValue - tempCorridor.TargetValue;
                            }
                            double? dehumidifyError = GetDehumidifyError(sensor, phase.HumidityCorridor);
                            if (dehumidifyError == null || !IsNearOverheat(sensor, tempCorridor))
                            {
                                return null;
                            }
                            return dehumidifyError;
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
                        break;
                }
            }
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
            AlertTriggered?.Invoke(sensorId, value);
        }
    }
}
