using System;
using System.Collections.Generic;
using System.Text;

namespace GreenhouseSimulator.Core.Models.Plans
{
    public class ParameterCorridor
    {
        public double MinValue { get; }
        public double MaxValue { get; }
        public double TargetValue { get; }

        public ParameterCorridor(double minValue, double maxValue, double targetValue)
        {
            MinValue = minValue;
            MaxValue = maxValue; 
            TargetValue = targetValue;
        }

        public bool IsWithinCorridor(double value)
        {
            return MinValue <= value && MaxValue >= value;
        }

        public double GetTolerance(double value)
        {
            return Math.Max(TargetValue - MinValue, MaxValue - TargetValue);
        }
    }
}
