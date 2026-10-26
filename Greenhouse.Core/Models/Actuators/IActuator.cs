using System;

namespace GreenhouseSimulator.Core.Models.Actuators
{
    public interface IActuator
    {
        bool IsOn { get; }
        Tuple<double, double> Location { get; }
        void MoveTo(Tuple<double, double> location);
        void TurnOn();
        void TurnOff();
    }
}
