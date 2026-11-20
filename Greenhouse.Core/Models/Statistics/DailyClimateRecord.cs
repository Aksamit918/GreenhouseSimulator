using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GreenhouseSimulator.Core.Models.Statistics
{
    public readonly record struct DailyClimateRecord (
        int DayNumber,
        double Mintemp,
        double AvgTemp,
        double MaxTemp,
        double TempCorridorMin,
        double TempCorridorMax,
        double MinHumidity,
        double AvgHumidity,
        double MaxHumidity,
        double HumidityCorridorMin,
        double HumidityCorridorMax)
    {

    }
}
