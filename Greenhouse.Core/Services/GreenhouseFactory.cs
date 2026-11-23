using GreenhouseSimulator.Core.Config;
using GreenhouseSimulator.Core.Controllers;
using GreenhouseSimulator.Core.Models;
using GreenhouseSimulator.Core.Models.Actuators;
using GreenhouseSimulator.Core.Models.Sensors;
using System;

namespace GreenhouseSimulator.Core.Services
{
    public static class GreenhouseFactory
    {
        public static (Greenhouse greenhouse, GreenhouseController controller) Create(string plansDirectory)
        {
            if (string.IsNullOrWhiteSpace(plansDirectory))
            {
                throw new ArgumentException("Plans directory cannot be empty.", nameof(plansDirectory));
            }

            var config = new SimulationConfig();
            var greenhouse = new Greenhouse(config);

            // Climate gear shares one zone: a sensor outside an actuator radius is never regulated.
            greenhouse.AddSensor(new ActiveSensor("temp-air", SensorType.Temperature, (5.5, 5.0)));
            greenhouse.AddSensor(new ActiveSensor("humidity-air", SensorType.Humidity, (4.0, 1.0)));
            greenhouse.AddSensor(new ActiveSensor("ph-tank", SensorType.Ph, (7.0, 2.0)));
            greenhouse.AddSensor(new ActiveSensor("ec-tank", SensorType.Ec, (8.0, 2.0)));

            greenhouse.AddActuator(new Heater((3.5, 4.0)));
            greenhouse.AddActuator(new AirConditioner((4.0, 4.0)));
            greenhouse.AddActuator(new Humidifier((4.0, 3.5)));
            greenhouse.AddActuator(new LightingSystem((6.0, 5.5)));
            greenhouse.AddActuator(new FertilizerDoser((7.5, 2.0)));

            var repository = new FilePlanRepository(plansDirectory);
            var controller = new GreenhouseController(greenhouse, repository);

            return (greenhouse, controller);
        }
    }
}
