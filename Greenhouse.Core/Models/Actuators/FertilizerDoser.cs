using System;

namespace GreenhouseSimulator.Core.Models.Actuators
{
    public class FertilizerDoser : IActuator
    {
        private readonly double _pHRate;
        private readonly double _nutrientRate;
        public bool IsOn { get; private set; }
        public Tuple<double, double> Location { get; }

        public double PhDelta => IsOn ? _pHRate : 0.0;
        public double NutrientDelta => IsOn ? _nutrientRate : 0.0;

        public FertilizerDoser(Tuple<double, double> location, double pHRate = 0.1, double nutrientRate = 20)
        {
            ArgumentNullException.ThrowIfNull(location);

            Location = location;
            _pHRate = pHRate;
            _nutrientRate = nutrientRate;
            IsOn = false;
        }

        public void TurnOn() => IsOn = true;

        public void TurnOff() => IsOn = false;
    }
}
