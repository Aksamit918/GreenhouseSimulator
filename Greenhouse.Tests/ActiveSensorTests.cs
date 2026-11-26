using GreenhouseSimulator.Core.Models.Sensors;
using Xunit;

namespace Greenhouse.Tests;

public class ActiveSensorTests
{
    [Fact]
    public void SensorWithoutCorridorNeverAlerts()
    {
        var sensor = new ActiveSensor("ec-tank", SensorType.Ec, (8, 2));
        int alerts = 0;
        sensor.OnThresholdExceeded += (_, _) => alerts++;

        sensor.UpdateMeasurement(1.4);

        Assert.Equal(0, alerts);
        Assert.False(sensor.IsInAlertState);
    }

    [Fact]
    public void SensorOutsideItsCorridorAlerts()
    {
        var sensor = new ActiveSensor("ph-tank", SensorType.Ph, (7, 2));
        int alerts = 0;
        sensor.OnThresholdExceeded += (_, _) => alerts++;
        sensor.SetThresholds(6.0, 0.5, 0.5);

        sensor.UpdateMeasurement(5.2);

        Assert.Equal(1, alerts);
        Assert.True(sensor.IsInAlertState);
    }
}
