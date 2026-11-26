using GreenhouseSimulator.Core.Config;
using GreenhouseSimulator.Core.Models.Actuators;
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

    [Fact]
    public void FansMixTheInteriorAndLeaveTheWallColder()
    {
        var config = new SimulationConfig();
        var grid = new EnvironmentGrid(config);
        grid.GetCell(10, 6).Temperature = 40.0;

        grid.ApplyAirCirculation();

        double interior = grid.GetCell(8, 4).Temperature;
        double wall = grid.GetCell(0, 0).Temperature;
        Assert.True(interior > wall);
        Assert.True(grid.GetCell(10, 6).Temperature < 32.0);
    }

    [Fact]
    public void NightTranspirationIsOnlyTheDarkResidual()
    {
        var config = new SimulationConfig();
        var grid = new EnvironmentGrid(config);
        double before = grid.GetCell(5, 3).AbsoluteHumidity;

        grid.StepPhysics(2.0, Array.Empty<Actuator>());

        double gain = grid.GetCell(5, 3).AbsoluteHumidity - before;
        Assert.InRange(gain, 0.02, 0.12);
    }

    [Fact]
    public void NoonSunAddsLightAcrossTheHall()
    {
        var grid = new EnvironmentGrid(new SimulationConfig());

        grid.ApplySolar(13.0, 1.0 / 12.0);

        Assert.True(grid.GetCell(1, 1).Light > 50.0);
        Assert.Equal(grid.GetCell(1, 1).Light, grid.GetCell(10, 5).Light, 3);
    }

    [Fact]
    public void RunningHeaterWarmsItsSurroundingsMostWithoutAddingHeat()
    {
        var plumeGrid = new EnvironmentGrid(new SimulationConfig { WallInsulationCoeff = 0.0 });
        var mixedGrid = new EnvironmentGrid(new SimulationConfig { WallInsulationCoeff = 0.0, PlumeResidenceHours = 0.0 });
        var plumeHeater = new Heater((5, 3));
        var mixedHeater = new Heater((5, 3));
        plumeHeater.TurnOn();
        mixedHeater.TurnOn();

        plumeGrid.StepPhysics(2.0, new List<Actuator> { plumeHeater });
        mixedGrid.StepPhysics(2.0, new List<Actuator> { mixedHeater });

        double plumeSpread = plumeGrid.GetCell(10, 6).Temperature - plumeGrid.GetCell(0, 0).Temperature;
        double mixedSpread = mixedGrid.GetCell(10, 6).Temperature - mixedGrid.GetCell(0, 0).Temperature;
        Assert.True(plumeSpread > 1.5);
        Assert.True(mixedSpread < 0.5);
        Assert.True(plumeGrid.GetCell(13, 6).Temperature > plumeGrid.GetCell(17, 6).Temperature);
        Assert.Equal(mixedGrid.GetAverageTemperature(), plumeGrid.GetAverageTemperature(), 1);
    }

    [Fact]
    public void SwitchedOffHeaterReturnsItsPlumeToTheHall()
    {
        var grid = new EnvironmentGrid(new SimulationConfig { WallInsulationCoeff = 0.0 });
        var heater = new Heater((5, 3));
        heater.TurnOn();
        grid.StepPhysics(2.0, new List<Actuator> { heater });

        heater.TurnOff();
        grid.StepPhysics(3.0, new List<Actuator> { heater });

        double spread = grid.GetCell(10, 6).Temperature - grid.GetCell(0, 0).Temperature;
        Assert.True(spread < 0.5);
    }

    [Fact]
    public void DoseSpreadsAlongTheSupply()
    {
        var grid = new EnvironmentGrid(new SimulationConfig());
        var doser = new FertilizerDoser((5, 2));
        doser.SetTargetPower(0.3);
        doser.UpdateInertia(1);

        grid.StepPhysics(12, new List<Actuator> { doser });

        double min = double.PositiveInfinity;
        double max = double.NegativeInfinity;
        for (int i = 0; i < grid.WidthCells; i++)
        {
            double ph = grid.GetCell(i, 2).Ph;
            min = Math.Min(min, ph);
            max = Math.Max(max, ph);
        }

        Assert.True(max - min < 0.2);
    }

    [Fact]
    public void PartialDoseOutrunsTheDrift()
    {
        var config = new SimulationConfig { InitialPh = 5.4 };
        var circuit = new SolutionCircuit(config, 20);
        var doser = new FertilizerDoser((7.5, 2));
        doser.SetTargetPower(0.3);
        doser.UpdateInertia(1);
        var actuators = new List<Actuator> { doser };

        for (int step = 0; step < 4 * config.SubstepsPerHour; step++)
        {
            circuit.Advance(1.0 / config.SubstepsPerHour, actuators);
        }

        Assert.InRange(circuit.PhAt(2, 1), 5.9, 6.4);
        Assert.InRange(circuit.PhAt(8, 5), 5.9, 6.4);
    }
}
