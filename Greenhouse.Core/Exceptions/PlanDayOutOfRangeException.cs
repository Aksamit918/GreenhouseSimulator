using System;
using System.Collections.Generic;
using System.Text;

namespace GreenhouseSimulator.Core.Exceptions
{
    public class PlanDayOutOfRangeException : Exception
    {
        public int RequestDay { get; }
        public string CropName { get; }

        public PlanDayOutOfRangeException(int day,  string cropName)
            : base($"Day {day} is outside the growing plan for '{cropName}'!")
        {
            RequestDay = day;
            CropName = cropName;
        }
    }
}
