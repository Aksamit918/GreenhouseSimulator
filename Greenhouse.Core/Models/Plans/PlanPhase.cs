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

        public const int TransitionHours = 2;

        // A sudden evening drop makes the AC chill the humid day air straight past the humidity ceiling.
        public ParameterCorridor GetTempCorridor(int hourOfDay)
        {
            if (IsDaytime(hourOfDay) || NightTempCorridor is null)
            {
                return TempCorridor;
            }

            int sinceDusk = (hourOfDay % 24) - DayDurationHours;
            if (sinceDusk >= TransitionHours)
            {
                return NightTempCorridor;
            }

            double share = (sinceDusk + 1.0) / (TransitionHours + 1.0);
            return new ParameterCorridor(
                TempCorridor.MinValue + share * (NightTempCorridor.MinValue - TempCorridor.MinValue),
                TempCorridor.MaxValue + share * (NightTempCorridor.MaxValue - TempCorridor.MaxValue),
                TempCorridor.TargetValue + share * (NightTempCorridor.TargetValue - TempCorridor.TargetValue));
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
