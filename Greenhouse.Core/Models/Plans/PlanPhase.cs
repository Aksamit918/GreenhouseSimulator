namespace GreenhouseSimulator.Core.Models.Plans
{
    public record PlanPhase(
        int DayNumber,
        ParameterCorridor TempCorridor,
        ParameterCorridor PhCorridor,
        ParameterCorridor HumidityCorridor,
        int LightDurationHours,
        ParameterCorridor? NightTempCorridor = null,
        int DayDurationHours = 24)
    {
        public bool IsDaytime(int hourOfDay)
        {
            int dayHours = DayDurationHours is < 1 or > 24 ? 24 : DayDurationHours;
            return NightTempCorridor is null || (hourOfDay % 24) < dayHours;
        }

        public ParameterCorridor GetTempCorridor(int hourOfDay)
        {
            if (IsDaytime(hourOfDay) || NightTempCorridor is null)
            {
                return TempCorridor;
            }

            return NightTempCorridor;
        }

        public void EnsureValid()
        {
            if (DayNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(DayNumber), "Day number starts at 1.");
            }

            if (LightDurationHours is < 0 or > 24)
            {
                throw new ArgumentOutOfRangeException(nameof(LightDurationHours), "Light duration must be from 0 to 24 hours.");
            }

            if (DayDurationHours is < 1 or > 24)
            {
                throw new ArgumentOutOfRangeException(nameof(DayDurationHours), "Day duration must be from 1 to 24 hours.");
            }

            ArgumentNullException.ThrowIfNull(TempCorridor);
            ArgumentNullException.ThrowIfNull(PhCorridor);
            ArgumentNullException.ThrowIfNull(HumidityCorridor);
        }
    }
}
