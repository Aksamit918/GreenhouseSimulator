namespace GreenhouseSimulator.Core.Models.Actuators
{
    public class LightingSystem : IActuator
    {
        public bool IsOn { get; private set; }
        public (double X, double Y) Location { get; private set; }

        public LightingSystem((double X, double Y) location)
        {
            Location = location;
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
