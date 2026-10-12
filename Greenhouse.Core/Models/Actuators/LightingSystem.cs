using System;

namespace GreenhouseSimulator.Core.Models.Actuators
{
    public class LightingSystem : IActuator
    {
        private const double LuxPerWatt = 25.0;
        private readonly int _maxPowerWatts;

        public bool IsOn { get; private set; }
        public Tuple<double, double> Location { get; }

        public double CurrentOutputLux => IsOn ? _maxPowerWatts * LuxPerWatt : 0.0;

        public LightingSystem(Tuple<double, double> location, int maxPowerWatts = 400)
        {
            ArgumentNullException.ThrowIfNull(location);
            if (maxPowerWatts <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPowerWatts), "Power must be positive.");
            }

            Location = location;
            _maxPowerWatts = maxPowerWatts;
            IsOn = false;
        }

        public void TurnOn() => IsOn = true;

        public void TurnOff() => IsOn = false;
    }
}
