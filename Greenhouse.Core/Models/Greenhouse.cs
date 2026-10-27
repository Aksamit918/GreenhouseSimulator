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

            ApplyNoise(phase, hourOfDay);
            ApplyActuatorEffects();
            ClampToPhysicalLimits();
            UpdateSensors();
        }

        private void ApplyNoise(PlanPhase phase, int hourOfDay)
        {
            CurrentTemperature = NextValue(
                CurrentTemperature,
                phase.GetTempCorridor(hourOfDay),
                TemperatureNoiseAmplitude,
                HasActuator<Heater>() && HasActuator<AirConditioner>());
            CurrentHumidity = NextValue(
                CurrentHumidity,
                phase.HumidityCorridor,
                HumidityNoiseAmplitude,
                HasActuator<Humidifier>() && HasActuator<Heater>());
            CurrentPh = NextValue(
                CurrentPh,
                phase.PhCorridor,
                PhNoiseAmplitude,
                HasActuator<FertilizerDoser>());
        }

        private static double NextValue(double value, ParameterCorridor corridor, double amplitude, bool regulated)
        {
            double next = value + RandomNoise(amplitude);
            if (!regulated)
                return next;

            double width = corridor.MaxValue - corridor.MinValue;
            double padding = CorridorPaddingFraction * width;
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
