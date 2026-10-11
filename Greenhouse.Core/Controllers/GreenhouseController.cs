using GreenhouseSimulator.Core.Models;
using GreenhouseSimulator.Core.Models.Actuators;
using GreenhouseSimulator.Core.Models.Plans;
using GreenhouseSimulator.Core.Models.Sensors;
using GreenhouseSimulator.Core.Services;
using System;
using System.Collections.Generic;

namespace GreenhouseSimulator.Core.Controllers
{
    public class GreenhouseController
    {
        public event Action<string, double>? AlertTriggered;

        private readonly Greenhouse _greenhouse;
        private readonly IPlanRepository _repository;
        private GrowingPlan? _activePlan;
        private int _currentDay;
        private bool _isRunning;

        public Greenhouse Greenhouse => _greenhouse;
        public GrowingPlan? ActivePlan => _activePlan;
        public int CurrentDay => _currentDay;
        public bool IsRunning => _isRunning;

        public GreenhouseController(Greenhouse greenhouse, IPlanRepository repository)
        {
            _greenhouse = greenhouse ?? throw new ArgumentNullException(nameof(greenhouse));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _activePlan = null;
            _currentDay = 0;
            _isRunning = false;

            SubscribeToActiveSensors();
        }

        public void StartCycle(string planName)
        {
            if (string.IsNullOrWhiteSpace(planName))
                throw new ArgumentException("Plan name cannot be empty.", nameof(planName));

            _activePlan = _repository.LoadPlan(planName);

            _currentDay = 1;
            _isRunning = true;

            RegulateEnvironment();
        }

        public void StopCycle()
        {
            _isRunning = false;

            foreach (var actuator in _greenhouse.GetAllActuators())
            {
                actuator.Stop();
            }
        }

        public void SimulationTick()
        {
            if (!_isRunning || _activePlan == null)
                return;

            try
            {
                RegulateEnvironment();

                if (_currentDay >= _activePlan.TotalDurationDays)
                {
                    StopCycle();
                    return;
                }

                _currentDay++;
            }
            catch (Exception)
            {
                StopCycle();
                throw;
            }
        }

        private void RegulateEnvironment()
        {
            if (_activePlan == null)
                return;

            PlanPhase currentPhase = _activePlan.GetPhaseForDay(_currentDay);

            foreach (var actuator in _greenhouse.GetAllActuators())
            {
                switch (actuator)
                {
                    case HeatingAndCoolingSystem heater:
                        heater.Regulate(currentPhase.TempCorridor.TargetValue);
                        break;

                    case VentilationSystem ventilation:
                        ventilation.Regulate(currentPhase.HumidityCorridor.TargetValue);
                        break;

                    case LightingSystem lighting:
                        lighting.Regulate(currentPhase.LightDurationHours > 0);
                        break;

                    case NutrientDosingSystem nutrientDoser:
                        nutrientDoser.Regulate(currentPhase.NutrientCorridor.TargetValue);
                        break;

                    case PhRegulationSystem phRegulator:
                        phRegulator.Regulate(currentPhase.PhCorridor.TargetValue);
                        break;
                }
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
    
