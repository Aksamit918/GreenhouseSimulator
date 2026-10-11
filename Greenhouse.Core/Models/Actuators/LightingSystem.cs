using System;
using System.Collections.Generic;
using System.Text;

namespace GreenhouseSimulator.Core.Models.Actuators
{
    public class LightingSystem : IActuator<bool>
    {
        private readonly int _maxPowerWatts;
        public bool IsOn { get; private set; }

        public LightingSystem(int maxPowerWatts)
        {
            _maxPowerWatts = maxPowerWatts;
            IsOn = false;
        }

        public void Regulate(bool targetState) => IsOn = targetState;

        public void Stop() => IsOn = false;
    }
}
