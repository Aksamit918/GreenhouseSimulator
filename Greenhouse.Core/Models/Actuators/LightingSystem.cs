using GreenhouseSimulator.Core.Models.Environment;

namespace GreenhouseSimulator.Core.Models.Actuators
{
    public class LightingSystem : Actuator
    {
        public double Efficiency { get; set; } = 0.8;
        public double HeatConversionFactor { get; set; } = 0.025;

        public LightingSystem((double X, double Y) location)
            : base(location, maxPower: 100.0, effectiveRadius: 3.0, powerConsumptionKw: 0.4)
        {
        }

        public override ClimateDelta GetImpactAt((double X, double Y) point, double dt, bool useGaussian = true)
        {
            if (CurrentPowerRatio <= 0.0)
            {
                return ClimateDelta.Zero;
            }

            double attenuation = GetAttenuation(point, useGaussian);
            double lightEffect = MaxPower * CurrentPowerRatio * Efficiency * attenuation;
            double heatEffect = MaxPower * CurrentPowerRatio * (1.0 - Efficiency) * HeatConversionFactor * attenuation * dt;
            return new ClimateDelta(heatEffect, 0.0, lightEffect, 0.0);
        }
    }
}
