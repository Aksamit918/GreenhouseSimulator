using GreenhouseSimulator.Core.Models;
using GreenhouseSimulator.Core.Models.Actuators;
using GreenhouseSimulator.Core.Models.Plans;
using GreenhouseSimulator.Core.Models.Sensors;
using GreenhouseSimulator.Core.Services;
using System;
using System.Linq;

namespace GreenhouseSimulator.Core.Controllers
{
    public class GreenhouseController
    {
        public event Action<string, double>? AlertTriggered;

        private readonly Greenhouse _greenhouse;
        private readonly IPlanRepository _repository;
        private GrowingPlan? _activePlan;
        private bool _isRunning;

        public Greenhouse Greenhouse => _greenhouse;
        public GrowingPlan? ActivePlan => _activePlan;
        public DateTime? StartDate { get; private set; }
        public int ElapsedHours { get; private set; }
        public int CurrentDay => ElapsedHours / 24 + 1;
        public int HourOfDay => ElapsedHours % 24;
        public bool IsRunning => _isRunning;

        public GreenhouseController(Greenhouse greenhouse, IPlanRepository repository)
        {
            _greenhouse = greenhouse ?? throw new ArgumentNullException(nameof(greenhouse));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _activePlan = null;
            ElapsedHours = 0;
            _isRunning = false;

            SubscribeToActiveSensors();
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

            PlanPhase phase = _activePlan.GetPhaseForDay(CurrentDay);
            ApplySensorThresholds(phase);
            RegulateEnvironment(phase);
        }

        public void StopCycle()
        {
            _isRunning = false;

            foreach (var actuator in _greenhouse.GetAllActuators())
            {
                actuator.TurnOff();
            }
        }

        public void SimulationTick()
        {
            if (!_isRunning || _activePlan == null)
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
                    StopCycle();
                }
            }
            catch (Exception)
            {
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
            bool nutrientsLow = _greenhouse.CurrentNutrientPpm < phase.NutrientCorridor.MinValue;
            bool nutrientsAtTarget = _greenhouse.CurrentNutrientPpm >= phase.NutrientCorridor.TargetValue;
            bool lightsOn = HourOfDay < phase.LightDurationHours;

            bool heaterOn = tooCold;
            bool acOn = tooHot;
            bool humidifierOn = tooDry;

            if (tooDry)
                heaterOn = false;

            if (tooHumid)
            {
                heaterOn = true;
                humidifierOn = false;
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
                        if (phHigh)
                        {
                            doser.TurnOff();
                        }
                        else if (phLow || nutrientsLow)
                        {
                            doser.TurnOn();
                        }
                        else if (phAtTarget && nutrientsAtTarget)
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
                    SensorType.Nutrient => phase.NutrientCorridor,
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

        private void SubscribeToActiveSensors()
        {
            foreach (var sensor in _greenhouse.GetAllSensors().OfType<ActiveSensor>())
            {
                sensor.OnThresholdExceeded += (sender, args) =>
                {
                    HandleSensorAlert(args.SensorId, args.CurrentValue);
                };
            }
        }

        private void HandleSensorAlert(string sensorId, double value)
        {
            AlertTriggered?.Invoke(sensorId, value);
        }
    }
}
