using GreenhouseSimulator.Core.Config;
using Xunit;
using GreenhouseSimulator.Core.Models.Environment;

namespace Greenhouse.Tests;

public class EnvironmentGridTests
{
    [Fact]
    public void CornerLosesHeatOnce()
    {
        var grid = new EnvironmentGrid(new SimulationConfig());

        grid.ApplyWallHeatLoss(0.0, 1.0);

        double corner = grid.GetCell(0, 0).Temperature;
        double edge = grid.GetCell(grid.WidthCells / 2, 0).Temperature;
        Assert.Equal(edge, corner, 3);
        Assert.True(corner < 22.0);
    }

    [Fact]
    public void DiffusionStepStaysInsideSpecifiedBand()
    {
        var config = new SimulationConfig();
        var grid = new EnvironmentGrid(config);
        grid.GetCell(10, 6).Temperature = 40.0;

        grid.ApplyDiffusion(config.HoursPerTick / config.SubstepsPerHour);

        double neighbor = grid.GetCell(11, 6).Temperature;
        double step = (neighbor - config.InitialTemperature) / ((40.0 + 3.0 * config.InitialTemperature) / 4.0 - config.InitialTemperature);
        Assert.InRange(step, 0.1, 0.3);
    }
}
