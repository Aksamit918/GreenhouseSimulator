using GreenhouseSimulator.Core.Models.Environment;

namespace GreenhouseSimulator.Core.Models.Actuators
{
    public abstract class Actuator
    {
        public (double X, double Y) Location { get; private set; }
        public double MaxPower { get; set; }
        public double TargetPowerRatio { get; private set; }
        public double CurrentPowerRatio { get; private set; }
        public double EffectiveRadius { get; set; }
        public double WarmupTimeHours { get; set; }
        public double CooldownTimeHours { get; set; }
        public double PowerConsumptionKw { get; set; }
        public double TotalEnergyKwh { get; private set; }
        public bool IsOn => TargetPowerRatio > 0.0;

        protected Actuator(
            (double X, double Y) location,
            double maxPower,
            double effectiveRadius,
            double powerConsumptionKw,
            double warmupTimeHours = 0.25,
            double cooldownTimeHours = 0.25)
        {
            Location = location;
            MaxPower = maxPower;
            EffectiveRadius = effectiveRadius;
            PowerConsumptionKw = powerConsumptionKw;
            WarmupTimeHours = warmupTimeHours;
            CooldownTimeHours = cooldownTimeHours;
        }

        public void MoveTo((double X, double Y) location) => Location = location;

        public void SetTargetPower(double ratio) => TargetPowerRatio = Math.Clamp(ratio, 0.0, 1.0);

        public void TurnOn() => SetTargetPower(1.0);

        public void TurnOff() => SetTargetPower(0.0);

        public virtual void UpdateInertia(double dt)
        {
            if (CurrentPowerRatio < TargetPowerRatio)
            {
                double step = WarmupTimeHours > 0 ? dt / WarmupTimeHours : 1.0;
                CurrentPowerRatio = Math.Min(TargetPowerRatio, CurrentPowerRatio + step);
            }
            else if (CurrentPowerRatio > TargetPowerRatio)
            {
                double step = CooldownTimeHours > 0 ? dt / CooldownTimeHours : 1.0;
                CurrentPowerRatio = Math.Max(TargetPowerRatio, CurrentPowerRatio - step);
            }

            TotalEnergyKwh += PowerConsumptionKw * CurrentPowerRatio * dt;
        }

        public double GetAttenuation((double X, double Y) point, bool useGaussian = true)
        {
            if (EffectiveRadius <= 0.0) return 0.0;

            double dx = point.X - Location.X;
            double dy = point.Y - Location.Y;
            double rSquared = dx * dx + dy * dy;
            double radiusSquared = EffectiveRadius * EffectiveRadius;

            if (useGaussian)
                return Math.Exp(-rSquared / (2.0 * radiusSquared));

            return 1.0 / (1.0 + rSquared / radiusSquared);
        }

        public abstract ClimateDelta GetImpactAt((double X, double Y) point, double dt, bool useGaussian = true);
    }
}
