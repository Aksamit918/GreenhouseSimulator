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

            ApplyNoise(phase, hourOfDay);
            ApplyActuatorEffects();
            ClampToPhysicalLimits();
            UpdateSensors();
        }

        private void ApplyNoise(PlanPhase phase, int hourOfDay)
        {
            CurrentTemperature = WalkWithin(CurrentTemperature, phase.GetTempCorridor(hourOfDay), TemperatureNoiseAmplitude);
            CurrentHumidity = WalkWithin(CurrentHumidity, phase.HumidityCorridor, HumidityNoiseAmplitude);
            CurrentPh = WalkWithin(CurrentPh, phase.PhCorridor, PhNoiseAmplitude);
        }

        private static double WalkWithin(double value, ParameterCorridor corridor, double amplitude)
        {
            double width = corridor.MaxValue - corridor.MinValue;
            double padding = CorridorPaddingFraction * width;
            double next = value + RandomNoise(amplitude);
            return Math.Clamp(next, corridor.MinValue - padding, corridor.MaxValue + padding);
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

        private void ClampToPhysicalLimits()
        {
            CurrentHumidity = Math.Clamp(CurrentHumidity, MinHumidity, MaxHumidity);
            CurrentPh = Math.Clamp(CurrentPh, MinPh, MaxPh);
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
