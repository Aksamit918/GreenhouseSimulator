using GreenhouseSimulator.Core.Models.Environment;

namespace GreenhouseSimulator.Core.Models.Actuators
{
    public class AirConditioner : Actuator
    {
        public double DehumidificationFactor { get; set; } = 0.6;

        public AirConditioner((double X, double Y) location)
            : base(location, maxPower: 4.0, effectiveRadius: 3.5, powerConsumptionKw: 2.5)
        {
        }

        public override ClimateDelta GetImpactAt((double X, double Y) point, double dt, bool useGaussian = true)
        {
            if (CurrentPowerRatio <= 0.0)
                return ClimateDelta.Zero;

            double effect = MaxPower * CurrentPowerRatio * GetAttenuation(point, useGaussian) * dt;
            return new ClimateDelta(-effect, -effect * DehumidificationFactor, 0.0, 0.0);
        }
    }
}
