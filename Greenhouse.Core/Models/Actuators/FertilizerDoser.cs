using GreenhouseSimulator.Core.Models.Environment;

namespace GreenhouseSimulator.Core.Models.Actuators
{
    public class FertilizerDoser : Actuator
    {
        public double TotalReagentConsumedMl { get; private set; }
        public double InjectionRateMlPerHour { get; set; } = 50.0;

        public FertilizerDoser((double X, double Y) location)
            : base(location, maxPower: 0.6, effectiveRadius: 2.0, powerConsumptionKw: 0.05)
        {
        }

        public override void UpdateInertia(double dt)
        {
            base.UpdateInertia(dt);
            TotalReagentConsumedMl += InjectionRateMlPerHour * CurrentPowerRatio * dt;
        }

        public override ClimateDelta GetImpactAt((double X, double Y) point, double dt, bool useGaussian = true)
        {
            if (CurrentPowerRatio <= 0.0)
                return ClimateDelta.Zero;

            double effect = MaxPower * CurrentPowerRatio * GetAttenuation(point, useGaussian) * dt;
            return new ClimateDelta(0.0, 0.0, 0.0, effect);
        }
    }
}
