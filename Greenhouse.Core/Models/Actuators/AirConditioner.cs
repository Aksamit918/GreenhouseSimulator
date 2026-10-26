using System;

namespace GreenhouseSimulator.Core.Models.Actuators
{
    public class AirConditioner : IActuator
    {
        private readonly double _temperatureRate;

        public bool IsOn { get; private set; }
        public Tuple<double, double> Location { get; private set; }

        public double TemperatureDelta => IsOn ? _temperatureRate : 0.0;

        public AirConditioner(Tuple<double, double> location, double temperatureRate = -1.5)
        {
            ArgumentNullException.ThrowIfNull(location);

            Location = location;
            _temperatureRate = temperatureRate;
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
