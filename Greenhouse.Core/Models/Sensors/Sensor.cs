using System;
using System.Collections.Generic;
using System.Text;

namespace GreenhouseSimulator.Core.Models.Sensors
{
    public abstract class Sensor
    {
        public string Id { get; }
        public SensorType Type { get; }
        public Tuple<double, double> Location { get; private set; }
        public double CurrentValue { get; protected set; }

        public Sensor(string id, SensorType type, Tuple<double, double> location)
        {
            Id = id;
            Type = type;
            Location = location;
            CurrentValue = 0.0;
        }

        public void MoveTo(Tuple<double, double> location)
        {
            ArgumentNullException.ThrowIfNull(location);
            Location = location;
        }

        public abstract void UpdateMeasurement(double value);
    }
}
