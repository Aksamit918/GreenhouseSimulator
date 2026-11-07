using System;
using System.Collections.Generic;
using System.Text;

namespace GreenhouseSimulator.Core.Models.Sensors
{
    public class ThresholdExceededEventArgs : EventArgs
    {
        public string SensorId { get; set; }
        public (double X, double Y) Location { get; set; }
        public double CurrentValue { get; set; }
        public double TargetValue { get; set; }
        public double Deviation { get; set; }
    }
}
