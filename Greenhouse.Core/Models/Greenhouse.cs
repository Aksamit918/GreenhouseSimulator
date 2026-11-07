using GreenhouseSimulator.Core.Config;
using GreenhouseSimulator.Core.Models.Environment;
using GreenhouseSimulator.Core.Models.Actuators;
using GreenhouseSimulator.Core.Models.Plans;
using GreenhouseSimulator.Core.Models.Sensors;
using GreenhouseSimulator.Core.Services;
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
        private readonly List<Actuator> _actuators;
        private readonly IRandomProvider _randomProvider;

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
            _actuators = new List<Actuator>();
            _randomProvider = new RandomProvider(config);
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

        public void AddActuator(Actuator actuator)
        {
            ArgumentNullException.ThrowIfNull(actuator);
            _actuators.Add(actuator);
        }

        public void RemoveActuator(Actuator actuator)
        {
            ArgumentNullException.ThrowIfNull(actuator);
            _actuators.Remove(actuator);
        }

        public List<Actuator> GetAllActuators() => new List<Actuator>(_actuators);

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
            foreach (Actuator actuator in _actuators)
            {
                if (actuator is TActuator)
                    return true;
            }

            return false;
        }

        public void Tick(PlanPhase phase, int hourOfDay)
        {
            ArgumentNullException.ThrowIfNull(phase);

            Grid.StepPhysics(hourOfDay, _actuators);
            UpdateSensors();
        }

        private void UpdateSensors()
        {
            foreach (var sensor in _sensors)
            {
                double noise = sensor.SensorNoiseSigma > 0.0
                    ? _randomProvider.GetNextGaussian(0.0, sensor.SensorNoiseSigma)
                    : 0.0;

                switch (sensor.Type)
                {
                    case SensorType.Temperature:
                        sensor.UpdateMeasurement(Grid.GetTemperatureAt(sensor.Location) + noise);
                        break;

                    case SensorType.Humidity:
                        sensor.UpdateMeasurement(Grid.GetHumidityAt(sensor.Location) + noise);
                        break;

                    case SensorType.Ph:
                        sensor.UpdateMeasurement(Grid.GetPhAt(sensor.Location) + noise);
                        break;

                    case SensorType.Light:
                        sensor.UpdateMeasurement(Grid.GetLightAt(sensor.Location) + noise);
                        break;
                }
            }
        }
    }
}
