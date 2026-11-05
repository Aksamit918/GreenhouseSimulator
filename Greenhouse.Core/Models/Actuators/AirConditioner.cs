namespace GreenhouseSimulator.Core.Models.Actuators
{
    public class AirConditioner : IActuator
    {
        private readonly double _temperatureRate;

        public bool IsOn { get; private set; }
        public (double X, double Y) Location { get; private set; }

        public double TemperatureDelta => IsOn ? _temperatureRate : 0.0;

        public AirConditioner((double X, double Y) location, double temperatureRate = -1.5)
        {
            Location = location;
            _temperatureRate = temperatureRate;
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
