using System;
using System.Collections.Generic;
using System.Text;

namespace GreenhouseSimulator.Core.Models.Actuators
{
    public enum CurrentOperatingState
    {
        Idle,
        Heating,
        Cooling
    }

    public class HeatingAndCoolingSystem : IActuator<double>
    {
        private const double Epsilon = 0.01;

        private readonly double _maxTemperatureChangeRate;
        public double? TargetTemperature { get; private set; }
        public CurrentOperatingState State { get; private set; }

        public HeatingAndCoolingSystem(double maxTemperatureChangeRate)
        {
            _maxTemperatureChangeRate = maxTemperatureChangeRate;
            TargetTemperature = null;
            State = CurrentOperatingState.Idle;
        }

        public void Regulate(double targetTemp) => TargetTemperature = targetTemp;

        public double CalculateTemperatureChange(double currentTemp)
        {
            if (!TargetTemperature.HasValue)
            {
                State = CurrentOperatingState.Idle;
                return 0.0;
            }

            double difference = TargetTemperature.Value - currentTemp;

            if (Math.Abs(difference) < Epsilon)
            {
                State = CurrentOperatingState.Idle;
                return 0.0;
            }
            else if (difference > 0)
            {
                State = CurrentOperatingState.Heating;
                return Math.Min(difference, _maxTemperatureChangeRate);
            }
            else
            {
                State = CurrentOperatingState.Cooling;
                return Math.Max(difference, -_maxTemperatureChangeRate);
            }
        }

        public void Stop()
        {
            TargetTemperature = null;
            State = CurrentOperatingState.Idle;
        }
    }
}
