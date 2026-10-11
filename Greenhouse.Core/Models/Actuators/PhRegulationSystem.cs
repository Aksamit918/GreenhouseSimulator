using System;
using System.Collections.Generic;
using System.Text;

namespace GreenhouseSimulator.Core.Models.Actuators
{
    public enum PhRegulationState
    {
        Idle,
        AddingAcid, 
        AddingBase  
    }

    public class PhRegulationSystem : IActuator<double>
    {
        private const double Epsilon = 0.05;

        private readonly double _maxPhAdjustmentRate;
        public double? TargetPh { get; private set; }
        public PhRegulationState State { get; private set; }

        public PhRegulationSystem(double maxPhAdjustmentRate)
        {
            _maxPhAdjustmentRate = maxPhAdjustmentRate;
            TargetPh = null;
            State = PhRegulationState.Idle;
        }

        public void Regulate(double targetPh) => TargetPh = targetPh;

        public double CalculatePhChange(double currentPh)
        {
            if (!TargetPh.HasValue)
            {
                State = PhRegulationState.Idle;
                return 0.0;
            }

            double difference = TargetPh.Value - currentPh;

            if (Math.Abs(difference) < Epsilon)
            {
                State = PhRegulationState.Idle;
                return 0.0;
            }
            else if (difference > 0)
            {
                State = PhRegulationState.AddingBase;
                return Math.Min(difference, _maxPhAdjustmentRate);
            }
            else
            {
                State = PhRegulationState.AddingAcid;
                return Math.Max(difference, -_maxPhAdjustmentRate);
            }
        }

        public void Stop()
        {
            TargetPh = null;
            State = PhRegulationState.Idle;
        }
    }
}
