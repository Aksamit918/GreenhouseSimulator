using GreenhouseSimulator.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GreenhouseSimulator.Core.Models.Plans
{
    public class GrowingPlan
    {
        public string CropName { get; set; } = string.Empty;

        public List<PlanPhase> Phases { get; set; } = new();

        public int TotalDurationDays => Phases.Count > 0 ? Phases.Max(p => p.DayNumber) : 0;

        public GrowingPlan()
        {
        }

        public GrowingPlan(string cropName)
        {
            if (string.IsNullOrWhiteSpace(cropName))
                throw new ArgumentException("Crop name cannot be empty.", nameof(cropName));

            CropName = cropName;
        }

        public void AddPhase(PlanPhase phase)
        {
            ArgumentNullException.ThrowIfNull(phase);
            Phases.Add(phase);
        }

        public PlanPhase GetPhaseForDay(int day)
        {
            if (day < 1)
                throw new ArgumentException("Day number cannot be less than 1!", nameof(day));

            return Phases.FirstOrDefault(p => p.DayNumber == day)
                ?? throw new PlanDayOutOfRangeException(day, CropName);
        }
    }
}
