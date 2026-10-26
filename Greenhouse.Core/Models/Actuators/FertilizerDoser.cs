using System;

namespace GreenhouseSimulator.Core.Models.Actuators
{
    public class FertilizerDoser : IActuator
    {
        private readonly double _pHRate;

        public bool IsOn { get; private set; }
        public Tuple<double, double> Location { get; private set; }

        public double PhDelta => IsOn ? _pHRate : 0.0;

        public FertilizerDoser(Tuple<double, double> location, double pHRate = 0.35)
        {
            ArgumentNullException.ThrowIfNull(location);

            Location = location;
            _pHRate = pHRate;
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
