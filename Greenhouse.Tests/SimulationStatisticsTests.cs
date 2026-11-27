using Xunit;
using GreenhouseSimulator.Core.Models.Plans;
using GreenhouseSimulator.Core.Models.Statistics;

namespace Greenhouse.Tests;

public class SimulationStatisticsTests
{
    [Fact]
    public void SlaCountersAreIndependent()
    {
        var stats = new SimulationStatistics();
        var temperature = new ParameterCorridor(20, 24, 22);
        var humidity = new ParameterCorridor(60, 80, 70);
        var ph = new ParameterCorridor(5.5, 6.5, 6);

        stats.RecordTick(22, 50, 6, 100, temperature, humidity, ph, true);
        stats.RecordTick(22, 70, 6, 0, temperature, humidity, ph, true);

        Assert.Equal(100.0, stats.TemperatureSlaPercent);
        Assert.Equal(50.0, stats.HumiditySlaPercent);
        Assert.Equal(100.0, stats.PhSlaPercent);
        Assert.Equal(50.0, stats.LightSlaPercent);
    }
}
