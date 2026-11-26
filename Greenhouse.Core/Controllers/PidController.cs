using GreenhouseSimulator.Core.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GreenhouseSimulator.Core.Controllers
{
    public class PidController
    {
        public double Kp { get; set; }
        public double Ki { get; set; }
        public double Kd { get; set; }

        private double _integral;
        private double _previousError;
        private bool _hasPreviousError;

        public PidController(double kp, double ki, double kd)
        {
            Kp = kp;
            Ki = ki;
            Kd = kd;

            _integral = 0.0;
            _previousError = 0.0;
            _hasPreviousError = false;
        }

        public PidController(SimulationConfig config) : this(config.PidKp, config.PidKi, config.PidKd) { }

        public double Compute(double error, double dt)
        {
            double maxIntegral = Ki > 0.0 ? 1.0 / Ki : 0.0;
            if (error > 0.0)
            {
                _integral = Math.Min(maxIntegral, _integral + error * dt);
            }
            else
            {
                _integral = 0.0;
            }

            double derivative = (_hasPreviousError && dt > 0.0) ? (error - _previousError) / dt : 0.0;
            _previousError = error;
            _hasPreviousError = true;

            double output = Kp * error + Ki * _integral + Kd * derivative;
            return Math.Clamp(output, 0.0, 1.0);
        }

        public void Reset()
        {
            _integral = 0.0;
            _previousError = 0.0;
            _hasPreviousError = false;
        }
    }
}
