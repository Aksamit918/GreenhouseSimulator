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
        public double SensorNoiseSigma { get; set; } = 0.05;

        public Sensor(string id, SensorType type, (double X, double Y) location, double sensorNoiseSigma = 0.05)
        {
            Id = id;
            Type = type;
            Location = location;
            CurrentValue = 0.0;
            SensorNoiseSigma = sensorNoiseSigma;
        }

        public void MoveTo((double X, double Y) location)
        {
            Location = location;
        }

        public virtual void ResetMeasurement()
        {
            CurrentValue = 0.0;
        }

        public abstract void UpdateMeasurement(double value);
    }
}
