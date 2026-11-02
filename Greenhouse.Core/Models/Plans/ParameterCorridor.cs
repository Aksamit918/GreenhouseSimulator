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
            if (!double.IsFinite(minValue) || !double.IsFinite(maxValue) || !double.IsFinite(targetValue))
            { 
                throw new ArgumentOutOfRangeException(nameof(targetValue), "Corridor values must be finite numbers.");
            }

            if (minValue >= maxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(maxValue), "Corridor max must be greater than min."); 
            }

            if (targetValue < minValue || targetValue > maxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(targetValue), "Target must lie inside the corridor."); 
            }

            MinValue = minValue;
            MaxValue = maxValue;
            TargetValue = targetValue;
        }

        public bool IsWithinCorridor(double value)
        {
            return MinValue <= value && MaxValue >= value;
        }

        public double GetToleranceBelow()
        {
            return TargetValue - MinValue;
        }

        public double GetToleranceAbove()
        {
            return MaxValue - TargetValue;
        }
    }
}
