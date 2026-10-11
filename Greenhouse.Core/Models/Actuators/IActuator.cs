using System;
using System.Collections.Generic;
using System.Text;

namespace GreenhouseSimulator.Core.Models.Actuators
{
    public interface IActuator
    {
        void Stop();
    }
    public interface IActuator<T> : IActuator
    {
        void Regulate(T targetValue);
    }
}
