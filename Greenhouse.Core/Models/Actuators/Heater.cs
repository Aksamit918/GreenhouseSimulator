using GreenhouseSimulator.Core.Models.Environment;

namespace GreenhouseSimulator.Core.Models.Actuators
{
    public class Heater : Actuator
    {
        public Heater((double X, double Y) location)
            : base(location, maxPower: 4.0, effectiveRadius: 3.5, powerConsumptionKw: 3.0)
        {
        }

        public override ClimateDelta GetImpactAt((double X, double Y) point, double dt, bool useGaussian = true)
        {
            if (CurrentPowerRatio <= 0.0)
                return ClimateDelta.Zero;

            double effect = MaxPower * CurrentPowerRatio * GetAttenuation(point, useGaussian) * dt;
            return new ClimateDelta(effect, 0.0, 0.0, 0.0);
        }
    }
}
