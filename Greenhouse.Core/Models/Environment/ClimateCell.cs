using GreenhouseSimulator.Core.Models.Actuators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GreenhouseSimulator.Core.Models.Environment
{
    public class ClimateCell
    {
        public double Temperature { get; set; } = 22.0;
        public double AbsoluteHumidity { get; set; } = 15.5;
        public double Light { get; set; } = 0.0;
        public double Ph {  get; set; } = 5.5;
        public double Ec { get; set; } = 1.5;

        public double RelativeHumidity => Psychrometrics.GetRelativeHumidity(Temperature, AbsoluteHumidity);

        public void CopyFrom(ClimateCell other)
        {
            Temperature = other.Temperature;
            AbsoluteHumidity = other.AbsoluteHumidity;
            Light = other.Light;
            Ph = other.Ph;
            Ec = other.Ec;
        }

        public void ApplyDelta(ClimateDelta delta)
        {
            Temperature += delta.DeltaTemp;
            AbsoluteHumidity += delta.DeltaAbsHumidity;
            Light += delta.DeltaLight;
            Ph += delta.DeltaPh;
        }

        public void EnforcePhysicalLimits(double minPh, double maxPh)
        {
            double maxAh = Psychrometrics.GetMaxAbsoluteHumidity(Temperature);
            AbsoluteHumidity = Math.Clamp(AbsoluteHumidity, 0.0, maxAh);
            Light = Math.Max(0.0, Light);
            Ph = Math.Clamp(Ph, minPh, maxPh);
            Ec = Math.Max(0.0, Ec);
        }
    }
}
