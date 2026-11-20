using GreenhouseSimulator.Core.Models.Environment;

namespace GreenhouseSimulator.Core.Models.Actuators
{
    public class Humidifier : Actuator
    {
        public double TotalWaterConsumedLiters { get; private set; }
        public double WaterFlowLitersPerHour { get; set; } = 2.0;
        public double AdiabaticCoolingFactor { get; set; } = 0.08;

        public Humidifier((double X, double Y) location)
            : base(location, maxPower: 5.0, effectiveRadius: 3.5, powerConsumptionKw: 0.3)
        {
        }

        public override void UpdateInertia(double dt)
        {
            base.UpdateInertia(dt);
            TotalWaterConsumedLiters += WaterFlowLitersPerHour * CurrentPowerRatio * dt;
        }

        public override void Reset()
        {
            base.Reset();
            TotalWaterConsumedLiters = 0.0;
        }

        public override ClimateDelta GetImpactAt((double X, double Y) point, double dt, bool useGaussian = true)
        {
            if (CurrentPowerRatio <= 0.0)
            {
                return ClimateDelta.Zero;
            }

            double effect = MaxPower * CurrentPowerRatio * GetAttenuation(point, useGaussian) * dt;
            return new ClimateDelta(-effect * AdiabaticCoolingFactor, effect, 0.0, 0.0);
        }
    }
}
