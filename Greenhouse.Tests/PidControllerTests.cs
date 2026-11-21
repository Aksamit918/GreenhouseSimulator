using GreenhouseSimulator.Core.Controllers;
using Xunit;

namespace Greenhouse.Tests;

public class PidControllerTests
{
    [Fact]
    public void HalfDegreeError_IsAboutTwentyPercent()
    {
        var pid = new PidController(0.4, 0.0, 0.0);

        double output = pid.Compute(0.5, 1.0);

        Assert.InRange(output, 0.18, 0.22);
    }

    [Fact]
    public void OutputStaysInsideZeroAndOne()
    {
        var pid = new PidController(0.4, 0.05, 0.02);

        Assert.Equal(0.0, pid.Compute(-4.0, 1.0));
        Assert.Equal(1.0, pid.Compute(20.0, 1.0));
    }
}
