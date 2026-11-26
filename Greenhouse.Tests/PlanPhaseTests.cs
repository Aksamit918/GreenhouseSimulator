using GreenhouseSimulator.Core.Models.Plans;
using Xunit;

namespace Greenhouse.Tests;

public class PlanPhaseTests
{
    [Theory]
    [InlineData(15, 28.0)]
    [InlineData(16, 26.0)]
    [InlineData(17, 24.0)]
    [InlineData(18, 22.0)]
    [InlineData(23, 22.0)]
    [InlineData(0, 28.0)]
    public void TemperatureTargetEasesIntoTheNight(int hour, double target)
    {
        var phase = new PlanPhase(
            DayNumber: 1,
            TempCorridor: new ParameterCorridor(26, 30, 28),
            PhCorridor: new ParameterCorridor(5.5, 6.5, 6),
            HumidityCorridor: new ParameterCorridor(60, 80, 70),
            LightDurationHours: 14,
            NightTempCorridor: new ParameterCorridor(20, 24, 22),
            DayDurationHours: 16);

        Assert.Equal(target, phase.GetTempCorridor(hour).TargetValue, 6);
    }

    [Fact]
    public void PhaseWithoutNightKeepsTheDayCorridor()
    {
        var day = new ParameterCorridor(26, 30, 28);
        var phase = new PlanPhase(1, day, new ParameterCorridor(5.5, 6.5, 6), new ParameterCorridor(60, 80, 70), 14);

        Assert.Same(day, phase.GetTempCorridor(0));
        Assert.Same(day, phase.GetTempCorridor(20));
    }
}
