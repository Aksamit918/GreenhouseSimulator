using System;
using System.Collections.Generic;
using System.Text;

namespace GreenhouseSimulator.Core.Models.Sensors
{
    public class ActiveSensor : Sensor
    {
        private double _targetValue;
        private double _toleranceBelow;
        private double _toleranceAbove;

        public double Hysteresis { get; set; } = 0.2;
        public bool IsInAlertState { get; private set; }

        public event EventHandler<ThresholdExceededEventArgs> OnThresholdExceeded;

        public ActiveSensor(string id, SensorType type, (double X, double Y) location)
       
            :base (id, type, location)
        { 
            _targetValue = 0.0;
            _toleranceBelow = 0.0;
            _toleranceAbove = 0.0;
        }

        public void SetThresholds(double target, double toleranceBelow, double toleranceAbove)
        {
            if (toleranceBelow < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(toleranceBelow), "Tolerance cannot be negative.");
            }

            if (toleranceAbove < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(toleranceAbove), "Tolerance cannot be negative.");
            }

            _targetValue = target;
            _toleranceBelow = toleranceBelow;
            _toleranceAbove = toleranceAbove;
        }

        public override void UpdateMeasurement(double newValue)
        {
            CurrentValue = newValue;

            double signedDeviation = newValue - _targetValue;

            if (!IsInAlertState)
            {
                IsInAlertState = signedDeviation < -_toleranceBelow || signedDeviation > _toleranceAbove;
            }
            else
            {
                double exitBelow = Math.Max(0.0, _toleranceBelow - Hysteresis);
                double exitAbove = Math.Max(0.0, _toleranceAbove - Hysteresis);
                if (signedDeviation >= -exitBelow && signedDeviation <= exitAbove)
                {
                    IsInAlertState = false;
                }
            }

            if (IsInAlertState)
            {
                var args = new ThresholdExceededEventArgs
                {
                    SensorId = Id,
                    Location = Location,
                    CurrentValue = newValue,
                    TargetValue = _targetValue,
                    Deviation = Math.Abs(signedDeviation)
                };

                OnThresholdExceeded?.Invoke(this, args);
            }
        }
    }
}
