using System;
using System.Collections.Generic;
using System.Text;

namespace GreenhouseSimulator.Core.Models.Sensors
{
    public class ActiveSensor : Sensor
    {
        private double _targetValue;
        private double _allowedTolerance;

        public event EventHandler<ThresholdExceededEventArgs> OnThresholdExceeded;

        public ActiveSensor(string id, SensorType type, Tuple<double, double> location)
       
            :base (id, type, location)
        { 
            _targetValue = 0.0;
            _allowedTolerance = 0.0;
        }

        public void SetThresholds(double target, double tolerance)
        {
            _targetValue = target;
            _allowedTolerance = tolerance;
        }

        public override void UpdateMeasurement(double newValue)
        {
            CurrentValue = newValue;

            double deviation = Math.Abs(newValue - _targetValue);

            if (deviation > _allowedTolerance)
            {
                var args = new ThresholdExceededEventArgs
                {
                    SensorId = Id,
                    CurrentValue = newValue,
                    TargetValue = _targetValue,
                    Deviation = deviation
                };

                OnThresholdExceeded?.Invoke(this, args);
            }
        }
    }
}
