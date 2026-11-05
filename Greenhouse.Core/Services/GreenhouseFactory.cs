using GreenhouseSimulator.Core.Controllers;
using GreenhouseSimulator.Core.Models;
using GreenhouseSimulator.Core.Models.Actuators;
using GreenhouseSimulator.Core.Models.Sensors;
using System;

namespace GreenhouseSimulator.Core.Services
{
    public static class GreenhouseFactory
    {
        public const double DefaultWidth = 10.0;
        public const double DefaultLength = 6.0;

        public static (Greenhouse greenhouse, GreenhouseController controller) Create(string plansDirectory)
        {
            if (string.IsNullOrWhiteSpace(plansDirectory))
                throw new ArgumentException("Plans directory cannot be empty.", nameof(plansDirectory));

            var greenhouse = new Greenhouse(DefaultWidth, DefaultLength);

            greenhouse.AddSensor(new ActiveSensor("temp-air", SensorType.Temperature, (2.0, 1.0)));
            greenhouse.AddSensor(new ActiveSensor("humidity-air", SensorType.Humidity, (2.0, 5.0)));
            greenhouse.AddSensor(new ActiveSensor("ph-tank", SensorType.Ph, (8.0, 2.0)));

            greenhouse.AddActuator(new Heater((1.0, 3.0)));
            greenhouse.AddActuator(new AirConditioner((9.0, 3.0)));
            greenhouse.AddActuator(new Humidifier((1.0, 5.0)));
            greenhouse.AddActuator(new LightingSystem((5.0, 5.5)));
            greenhouse.AddActuator(new FertilizerDoser((9.0, 5.0)));

            var repository = new FilePlanRepository(plansDirectory);
            var controller = new GreenhouseController(greenhouse, repository);

            return (greenhouse, controller);
        }
    }
}
