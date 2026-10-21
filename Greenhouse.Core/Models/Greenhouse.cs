using GreenhouseSimulator.Core.Models.Actuators;
using GreenhouseSimulator.Core.Models.Plans;
using GreenhouseSimulator.Core.Models.Sensors;
using System;
using System.Collections.Generic;

namespace GreenhouseSimulator.Core.Models
{
    public class Greenhouse
    {
        private const double DefaultAmbientTemperature = 20.0;
        private const double DefaultAmbientHumidity = 60.0;
        private const double DefaultWaterPh = 6.0;

        private const double TemperatureNoiseAmplitude = 0.6;
        private const double HumidityNoiseAmplitude = 2.5;
        private const double PhNoiseAmplitude = 0.08;
        private const double PhAcidificationPerTick = 0.12;
        private const double CorridorPaddingFraction = 0.1;

        private const double MinHumidity = 0.0;
        private const double MaxHumidity = 100.0;
        private const double MinPh = 0.0;
        private const double MaxPh = 14.0;

        public double Width { get; }
        public double Length { get; }
        public double CurrentTemperature { get; private set; }
        public double CurrentHumidity { get; private set; }
        public double CurrentPh { get; private set; }

        private readonly List<Sensor> _sensors;
        private readonly List<IActuator> _actuators;

        public Greenhouse(double width, double length)
        {
            if (width <= 0 || length <= 0)
                throw new ArgumentOutOfRangeException("Width and length must be positive.");

            Width = width;
            Length = length;

            _sensors = new List<Sensor>();
            _actuators = new List<IActuator>();

            CurrentTemperature = DefaultAmbientTemperature;
            CurrentHumidity = DefaultAmbientHumidity;
            CurrentPh = DefaultWaterPh;
        }

        public void AddSensor(Sensor sensor)
        {
            ArgumentNullException.ThrowIfNull(sensor);
            _sensors.Add(sensor);
        }

        public List<Sensor> GetAllSensors() => new List<Sensor>(_sensors);

        public void AddActuator(IActuator actuator)
        {
            ArgumentNullException.ThrowIfNull(actuator);
            _actuators.Add(actuator);
        }

        public List<IActuator> GetAllActuators() => new List<IActuator>(_actuators);

        public void Tick(PlanPhase phase, int hourOfDay)
        {
            ArgumentNullException.ThrowIfNull(phase);

            ApplyNoise();
            ApplyActuatorEffects();
            ClampEnvironment(phase, hourOfDay);
            UpdateSensors();
        }

        private void ApplyNoise()
        {
            CurrentTemperature += RandomNoise(TemperatureNoiseAmplitude);
            CurrentHumidity += RandomNoise(HumidityNoiseAmplitude);
            CurrentPh += RandomNoise(PhNoiseAmplitude) - PhAcidificationPerTick;
        }

        private static double RandomNoise(double amplitude)
        {
            return amplitude * (Random.Shared.NextDouble() * 2.0 - 1.0);
        }

        private void ApplyActuatorEffects()
        {
            foreach (var actuator in _actuators)
            {
                switch (actuator)
                {
                    case Heater heater:
                        CurrentTemperature += heater.TemperatureDelta;
                        CurrentHumidity += heater.HumidityDelta;
                        break;

                    case AirConditioner conditioner:
                        CurrentTemperature += conditioner.TemperatureDelta;
                        break;

                    case Humidifier humidifier:
                        CurrentHumidity += humidifier.HumidityDelta;
                        break;

                    case FertilizerDoser doser:
                        CurrentPh += doser.PhDelta;
                        break;
                }
            }
        }

        private void ClampEnvironment(PlanPhase phase, int hourOfDay)
        {
            CurrentTemperature = ClampToWideCorridor(CurrentTemperature, phase.GetTempCorridor(hourOfDay));
            CurrentHumidity = ClampToWideCorridor(CurrentHumidity, phase.HumidityCorridor);
            CurrentPh = ClampToWideCorridor(CurrentPh, phase.PhCorridor);

            CurrentHumidity = Math.Clamp(CurrentHumidity, MinHumidity, MaxHumidity);
            CurrentPh = Math.Clamp(CurrentPh, MinPh, MaxPh);
        }

        private static double ClampToWideCorridor(double value, ParameterCorridor corridor)
        {
            double width = corridor.MaxValue - corridor.MinValue;
            double padding = CorridorPaddingFraction * width;
            return Math.Clamp(value, corridor.MinValue - padding, corridor.MaxValue + padding);
        }

        private void UpdateSensors()
        {
            foreach (var sensor in _sensors)
            {
                switch (sensor.Type)
                {
                    case SensorType.Temperature:
                        sensor.UpdateMeasurement(CurrentTemperature);
                        break;

                    case SensorType.Humidity:
                        sensor.UpdateMeasurement(CurrentHumidity);
                        break;

                    case SensorType.Ph:
                        sensor.UpdateMeasurement(CurrentPh);
                        break;
                }
            }
        }
    }
}
