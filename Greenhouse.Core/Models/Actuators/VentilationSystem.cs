using System;
using System.Collections.Generic;
using System.Text;

namespace GreenhouseSimulator.Core.Models.Actuators
{
    public enum VentilationState
    {
        Idle,
        Dehumidifying,
        Humidifying
    }

    public class VentilationSystem : IActuator<double>
    {
        private const double Epsilon = 0.5;

        private readonly double _maxAirFlowRate;
        public double? TargetHumidity { get; private set; }
        public VentilationState State { get; private set; }

        public VentilationSystem(double maxAirFlowRate)
        {
            _maxAirFlowRate = maxAirFlowRate;
            TargetHumidity = null;
            State = VentilationState.Idle;
        }

        public void Regulate(double targetHumidity) => TargetHumidity = targetHumidity;

        public double CalculateHumidityChange(double currentHumidity)
        {
            if (!TargetHumidity.HasValue)
            {
                State = VentilationState.Idle;
                return 0.0;
            }

            double difference = TargetHumidity.Value - currentHumidity;

            if (Math.Abs(difference) < Epsilon)
            {
                State = VentilationState.Idle;
                return 0.0;
            }
            else if (difference > 0)
            {
                State = VentilationState.Humidifying;
                return Math.Min(difference, _maxAirFlowRate);
            }
            else
            {
                State = VentilationState.Dehumidifying;
                return Math.Max(difference, -_maxAirFlowRate);
            }
        }

        public void Stop()
        {
            TargetHumidity = null;
            State = VentilationState.Idle;
        }
    }
}
