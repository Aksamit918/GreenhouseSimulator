using GreenhouseSimulator.Core.Models.Environment;

namespace GreenhouseSimulator.Core.Models.Actuators
{
    public class FertilizerDoser : Actuator
    {
        public double TotalReagentConsumedMl { get; private set; }
        public double TotalAcidConsumedMl { get; private set; }
        public double InjectionRateMlPerHour { get; set; } = 50.0;
        public double AcidInjectionRateMlPerHour { get; set; } = 50.0;
        public double AcidTargetRatio { get; private set; }
        public double AcidPowerRatio { get; private set; }
        public override bool IsOn => base.IsOn || AcidTargetRatio > 0.0;

        public FertilizerDoser((double X, double Y) location)
            : base(location, maxPower: 0.6, effectiveRadius: 2.0, powerConsumptionKw: 0.05)
        {
        }

        public void SetAcidTarget(double ratio) => AcidTargetRatio = Math.Clamp(ratio, 0.0, 1.0);

        public override void UpdateInertia(double dt)
        {
            base.UpdateInertia(dt);

            if (AcidPowerRatio < AcidTargetRatio)
            {
                double step = WarmupTimeHours > 0 ? dt / WarmupTimeHours : 1.0;
                AcidPowerRatio = Math.Min(AcidTargetRatio, AcidPowerRatio + step);
            }
            else if (AcidPowerRatio > AcidTargetRatio)
            {
                double step = CooldownTimeHours > 0 ? dt / CooldownTimeHours : 1.0;
                AcidPowerRatio = Math.Max(AcidTargetRatio, AcidPowerRatio - step);
            }

            TotalReagentConsumedMl += InjectionRateMlPerHour * CurrentPowerRatio * dt;
            TotalAcidConsumedMl += AcidInjectionRateMlPerHour * AcidPowerRatio * dt;
        }

        public override void Reset()
        {
            base.Reset();
            AcidTargetRatio = 0.0;
            AcidPowerRatio = 0.0;
            TotalReagentConsumedMl = 0.0;
            TotalAcidConsumedMl = 0.0;
        }

        public override ClimateDelta GetImpactAt((double X, double Y) point, double dt, bool useGaussian = true)
        {
            return ClimateDelta.Zero;
        }
    }
}
