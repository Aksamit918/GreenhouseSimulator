using GreenhouseSimulator.Core.Config;
using Xunit;
using GreenhouseSimulator.Core.Models.Environment;
using GreenhouseSimulator.Core.Models.Plans;
using GreenhouseSimulator.Core.Models.Statistics;

namespace Greenhouse.Tests;

public class SimulationStatisticsTests
{
    [Fact]
    public void SlaCountersAreIndependentAndDayCloses()
    {
        var stats = new SimulationStatistics();
        var temperature = new ParameterCorridor(20, 24, 22);
        var humidity = new ParameterCorridor(60, 80, 70);
        var ph = new ParameterCorridor(5.5, 6.5, 6);

        stats.RecordTick(1, 0, 22, 50, 6, 100, temperature, humidity, ph, true);
        stats.RecordTick(1, 23, 22, 70, 6, 0, temperature, humidity, ph, true);

        Assert.Equal(100.0, stats.TemperatureSlaPercent);
        Assert.Equal(50.0, stats.HumiditySlaPercent);
        Assert.Equal(100.0, stats.PhSlaPercent);
        Assert.Equal(50.0, stats.LightSlaPercent);
        Assert.Single(stats.DailyHistory);
        Assert.Equal(22.0, stats.DailyHistory[0].AvgTemp);
        Assert.Equal(50.0, stats.DailyHistory[0].MinHumidity);
        Assert.Equal(70.0, stats.DailyHistory[0].MaxHumidity);
    }

    [Fact]
    public void ColdCornerProducesZoneAdvice()
    {
        var grid = new EnvironmentGrid(new SimulationConfig());
        grid.GetCell(0, grid.LengthCells - 1).Temperature = 5.0;
        var stats = new SimulationStatistics();

        var advice = stats.DescribeZones(
            grid,
            Array.Empty<GreenhouseSimulator.Core.Models.Actuators.Actuator>(),
            new ParameterCorridor(20, 24, 22),
            new ParameterCorridor(60, 80, 70));

        Assert.Contains(advice, line => line.Contains("холодная", StringComparison.Ordinal));
    }
}
