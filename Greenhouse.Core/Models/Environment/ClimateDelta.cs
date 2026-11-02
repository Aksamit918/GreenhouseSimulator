using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GreenhouseSimulator.Core.Models.Environment
{
    public readonly record struct ClimateDelta(
        double DeltaTemp,
        double DeltaAbsHumidity,
        double DeltaLight,
        double DeltaPh)
    {
        public static ClimateDelta Zero => new(0.0, 0.0, 0.0, 0.0);

        public static ClimateDelta operator +(ClimateDelta a, ClimateDelta b) =>
            new(
                a.DeltaTemp + b.DeltaTemp,
                a.DeltaAbsHumidity + b.DeltaAbsHumidity,
                a.DeltaLight + b.DeltaLight,
                a.DeltaPh + b.DeltaPh);
    }
}
