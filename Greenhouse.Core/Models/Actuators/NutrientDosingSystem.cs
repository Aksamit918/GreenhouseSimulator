using System;
using System.Collections.Generic;
using System.Text;

namespace GreenhouseSimulator.Core.Models.Actuators
{
    public enum NutrientDosingState
    {
        Idle,
        Dosing,   
        Diluting  
    }
    public class NutrientDosingSystem
    {
        private const double Epsilon = 5.0;
        private readonly double _maxDosingRate;
        public double? TargetPpm { get; private set; }
        public NutrientDosingState State { get; private set; }

        public NutrientDosingSystem(double maxDosingRate)
        {
            _maxDosingRate = maxDosingRate;
            TargetPpm = null;
            State = NutrientDosingState.Idle;
        }

        public void Regulate(double targetPpm) => TargetPpm = targetPpm;

        public double CalculatePpmChange(double currentPpm)
        {
            if (!TargetPpm.HasValue)
            {
                State = NutrientDosingState.Idle;
                return 0.0;
            }

            double difference = TargetPpm.Value - currentPpm;

            if (Math.Abs(difference) < Epsilon)
            {
                State = NutrientDosingState.Idle;
                return 0.0;
            }
            else if (difference > 0)
            {
                State = NutrientDosingState.Dosing;
                return Math.Min(difference, _maxDosingRate);
            }
            else
            {
                State = NutrientDosingState.Diluting;
                return Math.Max(difference, -_maxDosingRate);
            }
        }

        public void Stop()
        {
            TargetPpm = null;
            State = NutrientDosingState.Idle;
        }
    }
}

