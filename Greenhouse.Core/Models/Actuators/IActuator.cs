namespace GreenhouseSimulator.Core.Models.Actuators
{
    public interface IActuator
    {
        bool IsOn { get; }
        (double X, double Y) Location { get; }
        void MoveTo((double X, double Y) location);
        void TurnOn();
        void TurnOff();
    }
}
