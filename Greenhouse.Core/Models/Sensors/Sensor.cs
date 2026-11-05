using System.Collections.Generic;
using System.Text;

namespace GreenhouseSimulator.Core.Models.Sensors
{
    public abstract class Sensor
    {
        public string Id { get; }
        public SensorType Type { get; }
        public (double X, double Y) Location { get; private set; }
        public double CurrentValue { get; protected set; }

        public Sensor(string id, SensorType type, (double X, double Y) location)
        {
            Id = id;
            Type = type;
            Location = location;
            CurrentValue = 0.0;
        }

        public void MoveTo((double X, double Y) location)
        {
            Location = location;
        }

        public abstract void UpdateMeasurement(double value);
    }
}
