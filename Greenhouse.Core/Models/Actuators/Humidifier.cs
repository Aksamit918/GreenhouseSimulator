using System;

namespace GreenhouseSimulator.Core.Models.Actuators
{
    public class Humidifier : IActuator
    {
        private readonly double _humidityRate;

        public bool IsOn { get; private set; }
        public Tuple<double, double> Location { get; private set; }

        public double HumidityDelta => IsOn ? _humidityRate : 0.0;

        public Humidifier(Tuple<double, double> location, double humidityRate = 6.0)
        {
            ArgumentNullException.ThrowIfNull(location);

            Location = location;
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
