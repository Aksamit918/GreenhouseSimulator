namespace GreenhouseSimulator.Core.Models.Actuators
{
    public class Humidifier : IActuator
    {
        private readonly double _humidityRate;

        public bool IsOn { get; private set; }
        public (double X, double Y) Location { get; private set; }

        public double HumidityDelta => IsOn ? _humidityRate : 0.0;

        public Humidifier((double X, double Y) location, double humidityRate = 6.0)
        {
            Location = location;
            _humidityRate = humidityRate;
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
