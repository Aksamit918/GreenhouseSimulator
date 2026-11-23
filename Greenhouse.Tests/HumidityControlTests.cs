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
    public void WetAirFarBelowTheCeilingHeatsBeforeDrying()
    {
        (Heater heater, AirConditioner conditioner) = Regulate(21.0, 90.0);

        Assert.Equal(1.0, heater.TargetPowerRatio, 3);
        Assert.Equal(0.0, conditioner.TargetPowerRatio, 3);
    }

    [Fact]
    public void WetAirAtTheCeilingDriesAndReheats()
    {
        (Heater heater, AirConditioner conditioner) = Regulate(30.0, 90.0);

        Assert.Equal(1.0, heater.TargetPowerRatio, 3);
        Assert.Equal(1.0, conditioner.TargetPowerRatio, 3);
        Assert.Equal(0.0, NetHeat(heater, conditioner), 2);
    }

    [Fact]
    public void WetAirJustBelowTheCeilingKeepsALittleHeatWhileDrying()
    {
        (Heater heater, AirConditioner conditioner) = Regulate(29.5, 90.0);

        Assert.True(conditioner.TargetPowerRatio > 0.3);
        Assert.Equal(2.0, NetHeat(heater, conditioner), 2);
    }

    [Fact]
    public void DryHotAirCoolsWithoutReheat()
    {
        (Heater heater, AirConditioner conditioner) = Regulate(31.0, 70.0);

        Assert.Equal(0.0, heater.TargetPowerRatio, 3);
        Assert.Equal(1.0, conditioner.TargetPowerRatio, 2);
    }

    private static double NetHeat(Heater heater, AirConditioner conditioner)
    {
        return heater.TargetPowerRatio * heater.MaxPower - conditioner.TargetPowerRatio * conditioner.MaxPower;
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
