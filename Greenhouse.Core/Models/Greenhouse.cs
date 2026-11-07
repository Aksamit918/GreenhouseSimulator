using GreenhouseSimulator.Core.Config;
using GreenhouseSimulator.Core.Models.Environment;
using GreenhouseSimulator.Core.Models.Actuators;
using GreenhouseSimulator.Core.Models.Plans;
using GreenhouseSimulator.Core.Models.Sensors;
using System;
using System.Collections.Generic;

namespace GreenhouseSimulator.Core.Models
{
    public class Greenhouse
    {
        public SimulationConfig Config { get; }
        public EnvironmentGrid Grid { get; }

        public double Width { get; }
        public double Length { get; }
        public double CurrentTemperature => Grid.GetAverageTemperature();
        public double CurrentHumidity => Grid.GetAverageHumidity();
        public double CurrentPh => Grid.GetAveragePh();
        public double CurrentLight => Grid.GetAverageLight();

        private readonly List<Sensor> _sensors;
        private readonly List<IActuator> _actuators;

        public Greenhouse(SimulationConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            if (config.Width <= 0 || config.Length <= 0)
                throw new ArgumentOutOfRangeException(nameof(config), "Width and length must be positive.");

            Config = config;
            Width = config.Width;
            Length = config.Length;
            Grid = new EnvironmentGrid(config);

            _sensors = new List<Sensor>();
            _actuators = new List<IActuator>();
        }

        public void AddSensor(Sensor sensor)
        {
            ArgumentNullException.ThrowIfNull(sensor);
            _sensors.Add(sensor);
        }

        public void RemoveSensor(Sensor sensor)
        {
            ArgumentNullException.ThrowIfNull(sensor);
            _sensors.Remove(sensor);
        }

        public List<Sensor> GetAllSensors() => new List<Sensor>(_sensors);

        public void AddActuator(IActuator actuator)
        {
            ArgumentNullException.ThrowIfNull(actuator);
            _actuators.Add(actuator);
        }

        public void RemoveActuator(IActuator actuator)
        {
            ArgumentNullException.ThrowIfNull(actuator);
            _actuators.Remove(actuator);
        }

        public List<IActuator> GetAllActuators() => new List<IActuator>(_actuators);

        public string? DescribeMissingRegulators()
        {
            var missing = new List<string>();
            if (!HasActuator<Heater>())
                missing.Add("обогреватель");
            if (!HasActuator<AirConditioner>())
                missing.Add("кондиционер");
            if (!HasActuator<Humidifier>())
                missing.Add("увлажнитель");
            if (!HasActuator<FertilizerDoser>())
                missing.Add("дозатор удобрений");

            if (missing.Count == 0)
                return null;

            return "Нет регулирующих устройств: " + string.Join(", ", missing) + ". Показатели без них не регулировались.";
        }

        private bool HasActuator<TActuator>() where TActuator : class
        {
            foreach (IActuator actuator in _actuators)
            {
                if (actuator is TActuator)
                    return true;
            }

            return false;
        }

        public void Tick(PlanPhase phase, int hourOfDay)
        {
            ArgumentNullException.ThrowIfNull(phase);

            Grid.StepPhysics(hourOfDay);
            ApplyActuatorEffects();
            UpdateSensors();
        }

        private void ApplyActuatorEffects()
        {
            ClimateDelta delta = ClimateDelta.Zero;

            foreach (var actuator in _actuators)
            {
                switch (actuator)
                {
                    case Heater heater:
                        delta += new ClimateDelta(heater.TemperatureDelta, 0.0, 0.0, 0.0);
                        break;

                    case AirConditioner conditioner:
                        delta += new ClimateDelta(conditioner.TemperatureDelta, 0.0, 0.0, 0.0);
                        break;

                    case Humidifier humidifier:
                        delta += new ClimateDelta(0.0, humidifier.HumidityDelta, 0.0, 0.0);
                        break;

                    case FertilizerDoser doser:
                        delta += new ClimateDelta(0.0, 0.0, 0.0, doser.PhDelta);
                        doser.RecordConsumption(Config.DefaultDoserMlPerHour * Config.HoursPerTick);
                        break;
                }
            }

            Grid.ApplyGlobalDelta(delta);
        }

        private void UpdateSensors()
        {
            foreach (var sensor in _sensors)
            {
                switch (sensor.Type)
                {
                    case SensorType.Temperature:
                        sensor.UpdateMeasurement(Grid.GetTemperatureAt(sensor.Location));
                        break;

                    case SensorType.Humidity:
                        sensor.UpdateMeasurement(Grid.GetHumidityAt(sensor.Location));
                        break;

                    case SensorType.Ph:
                        sensor.UpdateMeasurement(Grid.GetPhAt(sensor.Location));
                        break;
                }
            }
        }
    }
}
