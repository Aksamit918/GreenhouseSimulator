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

        public double RelativeHumidity => Math.Clamp(
            AbsoluteHumidity / (216.7 * 6.112 * Math.Exp(17.67 * Temperature / (Temperature + 243.5)) / (Temperature + 273.15)) * 100,
            0.0,
            100);

        public void CopyFrom(ClimateCell other)
        {
            Temperature = other.Temperature;
            AbsoluteHumidity = other.AbsoluteHumidity;
            Light = other.Light;
            Ph = other.Ph;
        }

        public void ApplyDelta(ClimateDelta delta)
        {
            Temperature += delta.DeltaTemp;
            AbsoluteHumidity += delta.DeltaAbsHumidity;
            Light += delta.DeltaLight;
            Ph += delta.DeltaPh;
        }
    }
}
