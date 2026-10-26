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
        public event Action<string, double>? AlertTriggered;

        private readonly Greenhouse _greenhouse;
        private readonly IPlanRepository _repository;
        private readonly HashSet<ActiveSensor> _boundSensors = new();
        private GrowingPlan? _activePlan;
        private bool _isRunning;
        private bool _isPaused;

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
                throw new InvalidOperationException($"A growing plan for '{plan.CropName}' already exists.");

            _repository.SavePlan(plan);
        }

        public void UpdatePlan(GrowingPlan plan)
        {
            ArgumentNullException.ThrowIfNull(plan);
            plan.EnsureValid();

            if (!_repository.Contains(plan.CropName))
                throw new InvalidOperationException($"A growing plan for '{plan.CropName}' does not exist.");

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
                LastResult = SimulationResult.Stopped;

            _isRunning = false;
            _isPaused = false;

            foreach (var actuator in _greenhouse.GetAllActuators())
            {
                actuator.TurnOff();
            }
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
                throw new InvalidOperationException("Cannot pause a cycle that is not running.");

            _isPaused = true;
        }

        public void Resume()
        {
            if (!_isRunning)
                throw new InvalidOperationException("Cannot resume a cycle that is not running.");

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
                    LastResult = SimulationResult.Completed;
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

        private void RegulateEnvironment(PlanPhase phase)
        {
            ParameterCorridor tempCorridor = phase.GetTempCorridor(HourOfDay);
            bool tooCold = _greenhouse.CurrentTemperature < tempCorridor.MinValue;
            bool tooHot = _greenhouse.CurrentTemperature > tempCorridor.MaxValue;
            bool tooDry = _greenhouse.CurrentHumidity < phase.HumidityCorridor.MinValue;
            bool tooHumid = _greenhouse.CurrentHumidity > phase.HumidityCorridor.MaxValue;
            bool phLow = _greenhouse.CurrentPh < phase.PhCorridor.MinValue;
            bool phHigh = _greenhouse.CurrentPh > phase.PhCorridor.MaxValue;
            bool phAtTarget = _greenhouse.CurrentPh >= phase.PhCorridor.TargetValue;
            bool lightsOn = HourOfDay < phase.LightDurationHours;

            bool heaterOn = tooCold;
            bool acOn = tooHot;
            bool humidifierOn = tooDry;

            if (tooDry)
                heaterOn = false;

            if (tooHumid)
            {
                humidifierOn = false;
                if (!tooHot)
                    heaterOn = true;
            }

            foreach (var actuator in _greenhouse.GetAllActuators())
            {
                switch (actuator)
                {
                    case Heater heater:
                        SetPower(heater, heaterOn);
                        break;

                    case AirConditioner conditioner:
                        SetPower(conditioner, acOn);
                        break;

                    case Humidifier humidifier:
                        SetPower(humidifier, humidifierOn);
                        break;

                    case LightingSystem lighting:
                        SetPower(lighting, lightsOn);
                        break;

                    case FertilizerDoser doser:
                        if (phLow)
                        {
                            doser.TurnOn();
                        }
                        else if (phHigh || phAtTarget)
                        {
                            doser.TurnOff();
                        }
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

                sensor.SetThresholds(corridor.TargetValue, corridor.GetTolerance(corridor.TargetValue));
            }
        }

        private static void SetPower(IActuator actuator, bool on)
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

        private void SubscribeToActiveSensors()
        {
            foreach (var sensor in _greenhouse.GetAllSensors().OfType<ActiveSensor>())
                BindSensor(sensor);
        }

        private void BindSensor(ActiveSensor sensor)
        {
            if (!_boundSensors.Add(sensor))
                return;

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
