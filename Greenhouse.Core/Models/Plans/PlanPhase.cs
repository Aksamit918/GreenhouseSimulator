namespace GreenhouseSimulator.Core.Models.Plans
{
    public record PlanPhase(
        int DayNumber,
        ParameterCorridor TempCorridor,
        ParameterCorridor PhCorridor,
        ParameterCorridor HumidityCorridor,
        ParameterCorridor NutrientCorridor,
        int LightDurationHours,
        ParameterCorridor? NightTempCorridor = null,
        int DayDurationHours = 24)
    {
        public ParameterCorridor GetTempCorridor(int hourOfDay)
        {
            int dayHours = DayDurationHours is < 1 or > 24 ? 24 : DayDurationHours;
            bool isDaytime = (hourOfDay % 24) < dayHours;

            if (isDaytime || NightTempCorridor is null)
                return TempCorridor;

            return NightTempCorridor;
        }
    }
}
