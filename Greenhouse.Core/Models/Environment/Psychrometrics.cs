using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GreenhouseSimulator.Core.Models.Environment
{
    public static class Psychrometrics
    {
        public static double GetMaxAbsoluteHumidity(double temperature)
        {
            return 216.7 * 6.112 * Math.Exp(17.67 * temperature / (temperature + 243.5)) / (temperature + 273.15);
        }

        public static double GetRelativeHumidity(double temperature, double absoluteHumidity)
        {
            return Math.Clamp(absoluteHumidity * 100 / GetMaxAbsoluteHumidity(temperature), 0.0, 100.0);
        }

        public static double GetAbsoluteHumidity(double temperature, double relativeHumidity)
        {
            return GetMaxAbsoluteHumidity(temperature) * Math.Clamp(relativeHumidity, 0.0, 100.0) / 100.0;
        }
    }
}
