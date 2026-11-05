namespace GreenhouseSimulator.Core.Models.Actuators
{
    public class FertilizerDoser : IActuator
    {
        private readonly double _pHRate;

        public bool IsOn { get; private set; }
        public (double X, double Y) Location { get; private set; }

        public double PhDelta => IsOn ? _pHRate : 0.0;

        public FertilizerDoser((double X, double Y) location, double pHRate = 0.35)
        {
            Location = location;
            _pHRate = pHRate;
            IsOn = false;
        }

        public void MoveTo((double X, double Y) location)
        {
            Location = location;
        }

        public void TurnOn() => IsOn = true;

        public void TurnOff() => IsOn = false;
    }
}
