using System;

namespace GreenhouseSimulator.Core.Models.Actuators
{
    public class LightingSystem : IActuator
    {
        public bool IsOn { get; private set; }
        public Tuple<double, double> Location { get; private set; }

        public LightingSystem(Tuple<double, double> location)
        {
            ArgumentNullException.ThrowIfNull(location);

            Location = location;
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
