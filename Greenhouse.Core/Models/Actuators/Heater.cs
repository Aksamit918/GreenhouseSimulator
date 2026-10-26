using System;

namespace GreenhouseSimulator.Core.Models.Actuators
{
    public class Heater : IActuator
    {
        private readonly double _temperatureRate;
        private readonly double _humidityRate;

        public bool IsOn { get; private set; }
        public Tuple<double, double> Location { get; private set; }

        public double TemperatureDelta => IsOn ? _temperatureRate : 0.0;
        public double HumidityDelta => IsOn ? _humidityRate : 0.0;

        public Heater(Tuple<double, double> location, double temperatureRate = 1.5, double humidityRate = -4.0)
        {
            ArgumentNullException.ThrowIfNull(location);

            Location = location;
            _temperatureRate = temperatureRate;
            _humidityRate = humidityRate;
            IsOn = false;
        }

        public void MoveTo(Tuple<double, double> location)
        {
            ArgumentNullException.ThrowIfNull(location);
            Location = location;
        }

        public void TurnOn() => IsOn = true;

        public void TurnOff() => IsOn = false;
    }
}
