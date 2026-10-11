using GreenhouseSimulator.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace GreenhouseSimulator.Core.Models.Plans
{
    public class GrowingPlan
    {
        public string CropName { get; }

        private readonly List<PlanPhase> _phases;
        public int TotalDurationDays => _phases.Count > 0 ? _phases.Max(p => p.DayNumber) : 0;

        public GrowingPlan(string cropName)
        {
            CropName = cropName;
            _phases = new List<PlanPhase>();
        }

        public void AddPhase(PlanPhase phase)
        {
            if (phase == null)
            {
                throw new ArgumentNullException(nameof(phase));
            }

            _phases.Add(phase);
        }

        public PlanPhase GetPhaseForDay(int day)
        {
            if (day < 1)
            {
                throw new ArgumentException("Day number cannot be less than 1!", nameof(day));
            }

            return _phases.FirstOrDefault(p => p.DayNumber == day)
                ?? throw new PlanDayOutOfRangeException(day, CropName);
        }
    }
}
