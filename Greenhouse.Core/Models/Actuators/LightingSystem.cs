using System;

namespace GreenhouseSimulator.Core.Models.Actuators
{
    public class LightingSystem : IActuator
    {
        public bool IsOn { get; private set; }
        public Tuple<double, double> Location { get; }

        public LightingSystem(Tuple<double, double> location)
        {
            ArgumentNullException.ThrowIfNull(location);

            Location = location;
            IsOn = false;
        }

        public void TurnOn() => IsOn = true;

        public void TurnOff() => IsOn = false;
    }
}
