using GreenhouseSimulator.Core.Models.Actuators;
using GreenhouseSimulator.Core.Models.Sensors;
using System;
using System.Collections.Generic;
using System.Text;

namespace GreenhouseSimulator.Core.Models
{
    public class Greenhouse
    {
        public double Width { get; }
        public double Length { get; }
        private readonly List<Sensor> _sensors;
        private readonly List<IActuator> _actuators;

        public Greenhouse(double width, double length)
        {
            if (width <= 0 || length <= 0)
                throw new ArgumentOutOfRangeException("Width and length must be greater than zero.");

            Width = width;
            Length = length;
            _sensors = new List<Sensor>();
            _actuators = new List<IActuator>();
        }

        public void AddSensor(Sensor sensor)
        {
            ArgumentNullException.ThrowIfNull(sensor);
            _sensors.Add(sensor);
        }

        public List<Sensor> GetAllSensors() => new List<Sensor>(_sensors);

        public void AddActuator(IActuator actuator)
        {
            ArgumentNullException.ThrowIfNull(actuator);
            _actuators.Add(actuator);
        }

        public List<IActuator> GetAllActuators() => new List<IActuator>(_actuators);
    }
}
