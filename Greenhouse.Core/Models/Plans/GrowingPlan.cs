using GreenhouseSimulator.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
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
            phase.EnsureValid();

            if (Phases.Any(existing => existing.DayNumber == phase.DayNumber))
                throw new ArgumentException($"Day {phase.DayNumber} is already in the plan.", nameof(phase));

            Phases.Add(phase);
        }

        public void EnsureValid()
        {
            if (string.IsNullOrWhiteSpace(CropName))
                throw new ArgumentException("Crop name cannot be empty.", nameof(CropName));

            CropName = CropName.Trim();

            if (CropName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Crop name cannot contain path or file-name characters.", nameof(CropName));

            if (Phases == null || Phases.Count == 0)
                throw new ArgumentException("A growing plan needs at least one day.", nameof(Phases));

            var days = new HashSet<int>();
            foreach (PlanPhase phase in Phases)
            {
                ArgumentNullException.ThrowIfNull(phase);
                phase.EnsureValid();

                if (!days.Add(phase.DayNumber))
                    throw new ArgumentException($"Day {phase.DayNumber} is listed more than once.", nameof(Phases));
            }

            for (int day = 1; day <= Phases.Count; day++)
            {
                if (!days.Contains(day))
                    throw new ArgumentException($"Days must be numbered 1 through {Phases.Count} without gaps. Missing day {day}.", nameof(Phases));
            }
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
