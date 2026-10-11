using System;
using System.Collections.Generic;
using System.Text;

namespace GreenhouseSimulator.Core.Models.Plans
{
    public record PlanPhase(
        int DayNumber,
        ParameterCorridor TempCorridor,
        ParameterCorridor PhCorridor,
        ParameterCorridor HumidityCorridor,
        ParameterCorridor NutrientCorridor,
        int LightDurationHours
        );
}
