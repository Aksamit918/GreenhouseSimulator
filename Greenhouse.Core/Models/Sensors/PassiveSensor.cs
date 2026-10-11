using System;
using System.Collections.Generic;
using System.Text;

namespace GreenhouseSimulator.Core.Models.Sensors
{
    public class PassiveSensor : Sensor
    {
        public PassiveSensor(string id, SensorType type, Tuple<double, double> location)
            : base(id, type, location) { }
        public override void UpdateMeasurement(double newValue)
        {
            CurrentValue = newValue;
        }
    }
}
