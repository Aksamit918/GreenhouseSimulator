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

            greenhouse.AddSensor(new ActiveSensor("temp-air", SensorType.Temperature, Tuple.Create(2.0, 1.0)));
            greenhouse.AddSensor(new ActiveSensor("humidity-air", SensorType.Humidity, Tuple.Create(2.0, 5.0)));
            greenhouse.AddSensor(new ActiveSensor("ph-tank", SensorType.Ph, Tuple.Create(8.0, 2.0)));
            greenhouse.AddSensor(new ActiveSensor("nutrient-tank", SensorType.Nutrient, Tuple.Create(8.0, 4.0)));
            greenhouse.AddSensor(new PassiveSensor("light-canopy", SensorType.Light, Tuple.Create(5.0, 1.0)));

            greenhouse.AddActuator(new Heater(Tuple.Create(1.0, 3.0)));
            greenhouse.AddActuator(new AirConditioner(Tuple.Create(9.0, 3.0)));
            greenhouse.AddActuator(new Humidifier(Tuple.Create(1.0, 5.0)));
            greenhouse.AddActuator(new LightingSystem(Tuple.Create(5.0, 5.5)));
            greenhouse.AddActuator(new FertilizerDoser(Tuple.Create(9.0, 5.0)));

            var repository = new FilePlanRepository(plansDirectory);
            var controller = new GreenhouseController(greenhouse, repository);

            return (greenhouse, controller);
        }
    }
}
