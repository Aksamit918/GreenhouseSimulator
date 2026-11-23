using GreenhouseSimulator.Core.Config;
using Xunit;
using GreenhouseSimulator.Core.Models.Biology;
using GreenhouseSimulator.Core.Models.Environment;
using GreenhouseSimulator.Core.Models.Plans;

namespace Greenhouse.Tests;

public class CropGrowthTests
{
    [Fact]
    public void GddDoesNotAccrueOutsideCorridor()
    {
        var config = new SimulationConfig { TempStressFactor = 0.0, HumidityStressFactor = 0.0, PhStressFactor = 0.0 };
        var grid = new EnvironmentGrid(config);
        Fill(grid, 40.0, 70.0, 6.0, 0.0);
        var crop = new CropGrowthModel(config, grid.WidthCells, grid.LengthCells);

        crop.Advance(grid, Phase(), 12, 1, 1.0);

        Assert.Equal(0.0, crop.AccumulatedGdd);
        Assert.Equal(0.0, crop.YieldPotential);
    }

    [Fact]
    public void BiomassGrowsFromGddInsideCorridor()
    {
        var config = new SimulationConfig();
        var grid = new EnvironmentGrid(config);
        Fill(grid, 22.0, 70.0, 6.0, 100.0);
        var crop = new CropGrowthModel(config, grid.WidthCells, grid.LengthCells);
        crop.SetPlannedDuration(1);

        crop.Advance(grid, Phase(), 12, 1, 1.0);

        Assert.True(crop.AccumulatedGdd > 0.5);
        Assert.InRange(crop.YieldPotential, 4.0, 5.0);
    }

    [Fact]
    public void StressStopsAfterYieldReachesZero()
    {
        var config = new SimulationConfig { TempStressFactor = 1.0, HumidityStressFactor = 0.0, PhStressFactor = 0.0 };
        var grid = new EnvironmentGrid(config);
        Fill(grid, 22.0, 70.0, 6.0, 100.0);
        var crop = new CropGrowthModel(config, grid.WidthCells, grid.LengthCells);
        crop.SetPlannedDuration(24);
        crop.Advance(grid, Phase(), 12, 1, 1.0);
        double grown = crop.YieldPotential;
        Assert.True(grown > 0.0);

        Fill(grid, 10.0, 70.0, 6.0, 0.0);
        crop.Advance(grid, Phase(), 13, 1, 1.0);
        Assert.Equal(0.0, crop.YieldPotential);
        double stress = crop.TotalStressIndex;

        crop.Advance(grid, Phase(), 14, 1, 1.0);

        Assert.Equal(0.0, crop.YieldPotential);
        Assert.Equal(stress, crop.TotalStressIndex);
    }

    [Fact]
    public void UniformityUsesClimateSpread()
    {
        var config = new SimulationConfig();
        var grid = new EnvironmentGrid(config);
        Fill(grid, 22.0, 70.0, 6.0, 0.0);
        var crop = new CropGrowthModel(config, grid.WidthCells, grid.LengthCells);
        crop.Advance(grid, Phase(), 12, 1, 1.0);
        double even = crop.UniformityIndex;

        for (int i = 0; i < grid.WidthCells; i++)
        {
            for (int j = 0; j < grid.LengthCells; j++)
            {
                grid.GetCell(i, j).Temperature = i < grid.WidthCells / 2 ? 5.0 : 35.0;
            }
        }

        crop.Advance(grid, Phase(), 13, 1, 1.0);

        Assert.True(even > 90.0);
        Assert.True(crop.UniformityIndex < 40.0);
    }

    [Fact]
    public void MildColdSlowsGrowthWithoutErasingBiomass()
    {
        var config = new SimulationConfig();
        var grid = new EnvironmentGrid(config);
        Fill(grid, 22.0, 70.0, 6.0, 100.0);
        var crop = new CropGrowthModel(config, grid.WidthCells, grid.LengthCells);
        crop.SetPlannedDuration(1);
        crop.Advance(grid, Phase(), 12, 1, 1.0);

        Fill(grid, 19.0, 70.0, 6.0, 100.0);
        crop.Advance(grid, Phase(), 13, 1, 1.0);

        Assert.True(crop.YieldPotential > 4.0);
        Assert.Equal(0.0, crop.TotalStressIndex);
    }

    [Fact]
    public void DarknessStoresHeatTimeWithoutBiomass()
    {
        var config = new SimulationConfig();
        var grid = new EnvironmentGrid(config);
        Fill(grid, 22.0, 70.0, 6.0, 0.0);
        var crop = new CropGrowthModel(config, grid.WidthCells, grid.LengthCells);
        crop.SetPlannedDuration(1);

        crop.Advance(grid, Phase(), 12, 1, 1.0);

        Assert.True(crop.AccumulatedGdd > 0.5);
        Assert.Equal(0.0, crop.YieldPotential);
    }

    private static PlanPhase Phase()
    {
        return new PlanPhase(
            1,
            new ParameterCorridor(20, 24, 22),
            new ParameterCorridor(5.5, 6.5, 6),
            new ParameterCorridor(60, 80, 70),
            14);
    }

    private static void Fill(EnvironmentGrid grid, double temperature, double humidity, double ph, double light)
    {
        for (int i = 0; i < grid.WidthCells; i++)
        {
            for (int j = 0; j < grid.LengthCells; j++)
            {
                var cell = grid.GetCell(i, j);
                cell.Temperature = temperature;
                cell.AbsoluteHumidity = Psychrometrics.GetAbsoluteHumidity(temperature, humidity);
                cell.Ph = ph;
                cell.Light = light;
            }
        }
    }
}
