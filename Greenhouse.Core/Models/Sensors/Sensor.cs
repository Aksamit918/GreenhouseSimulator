using System;
using System.Collections.Generic;
using System.Text;

namespace GreenhouseSimulator.Core.Models.Sensors
{
    public abstract class Sensor
    {
        public string Id { get; }
        public SensorType Type { get; }
        public Tuple<double, double> Location { get; }
        public double CurrentValue { get; protected set; }

        public Sensor(string id, SensorType type, Tuple<double, double> location)
        {
            Id = id;
            Type = type;
            Location = location;
            CurrentValue = 0.0;
        }

        public abstract void UpdateMeasurement(double value);
    }
}
