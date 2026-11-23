using GreenhouseSimulator.Core.Config;
using GreenhouseSimulator.Core.Controllers;
using GreenhouseModel = GreenhouseSimulator.Core.Models.Greenhouse;
using GreenhouseSimulator.Core.Models.Actuators;
using GreenhouseSimulator.Core.Models.Environment;
using GreenhouseSimulator.Core.Models.Sensors;
using GreenhouseSimulator.Core.Services;
using Xunit;

namespace Greenhouse.Tests;

public class HumidityControlTests
{
    [Fact]
    public void WetAirBelowTargetHeatsAndDoesNotCool()
    {
        (Heater heater, AirConditioner conditioner) = Regulate(21.0, 90.0);

        Assert.True(heater.TargetPowerRatio > 0.5);
        Assert.Equal(0.0, conditioner.TargetPowerRatio);
    }

    [Fact]
    public void WetAirAboveTargetCoolsOnlyDownToTheSetpoint()
    {
        (Heater heater, AirConditioner conditioner) = Regulate(29.0, 90.0);

        Assert.True(heater.TargetPowerRatio > 0.0);
        Assert.InRange(conditioner.TargetPowerRatio, 0.05, 0.25);
    }

    private static (Heater heater, AirConditioner conditioner) Regulate(double temperature, double relativeHumidity)
    {
        var config = new SimulationConfig
        {
            InitialTemperature = temperature,
            InitialAbsoluteHumidity = Psychrometrics.GetAbsoluteHumidity(temperature, relativeHumidity)
        };
        var greenhouse = new GreenhouseModel(config);
        var thermometer = new ActiveSensor("temp-air", SensorType.Temperature, (5, 3)) { SensorNoiseSigma = 0 };
        var hygrometer = new ActiveSensor("humidity-air", SensorType.Humidity, (5, 3)) { SensorNoiseSigma = 0 };
        var heater = new Heater((5, 3));
        var conditioner = new AirConditioner((5, 3));
        greenhouse.AddSensor(thermometer);
        greenhouse.AddSensor(hygrometer);
        greenhouse.AddActuator(heater);
        greenhouse.AddActuator(conditioner);
        greenhouse.AddActuator(new Humidifier((5, 3)));
        greenhouse.AddActuator(new FertilizerDoser((5, 2)));

        string plans = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Greenhouse.Core", "Plans"));
        var controller = new GreenhouseController(greenhouse, new FilePlanRepository(plans));
        controller.StartCycle("Tulips");
        return (heater, conditioner);
    }
}
