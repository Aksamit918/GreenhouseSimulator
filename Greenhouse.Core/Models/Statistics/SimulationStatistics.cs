using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GreenhouseSimulator.Core.Models.Statistics
{
    public class SimulationStatistics
    {
        public int TotalTicks { get; private set; }
        public int TotalAlertsCount { get; private set; }
        private int _tempOkTicks;
        private int _humidityOkTicks;
        private int _phOkTicks;
        private int _lightOkTicks;

        public double TemperatureSlaPercent => TotalTicks > 0 ? _tempOkTicks * 100.0 / TotalTicks : 100.0;
        public double HumiditySlaPercent => TotalTicks > 0 ? _tempOkTicks * 100.0 / TotalTicks : 100.0;
        public double PhSlaPercent => TotalTicks > 0 ? _tempOkTicks * 100.0 / TotalTicks : 100.0;
        public double LightSlaPercent => TotalTicks > 0 ? _tempOkTicks * 100.0 / TotalTicks : 100.0;

        private readonly List<DailyClimateRecord> _dailyHistory = new();
        public IReadOnlyList<DailyClimateRecord> DailyHistory => _dailyHistory;

        private int _currentDay;
        private int _hoursInCurrentDay;
        private double _dayMinTemp;
        private double _dayMaxTemp;
        private double _daySumTemp;
        private double _dayMinHum;
        private double _dayMaxHum;
        private double _daySumHum;
        private double _dayTempCorridorMin;
        private double _dayTempCorridorMax;
        private double _dayHumidityCorridorMin;
        private double _dayHumidityCorridorMax;

        public SimulationStatistics()
        {
            Reset();
        }

        public void Reset()
        {
            TotalTicks = 0;
            TotalAlertsCount = 0;
            _tempOkTicks = 0;
            _humidityOkTicks = 0;
            _phOkTicks = 0;
            _lightOkTicks = 0;

            _dailyHistory.Clear();

            _currentDay = 0;
            _hoursInCurrentDay = 0;
            _dayMinTemp = 0.0;
            _dayMaxTemp = 0.0;
            _daySumTemp = 0.0;
            _dayMinHum = 0.0;
            _dayMaxHum = 0.0;
            _daySumHum = 0.0;
            _dayTempCorridorMin = 0.0;
            _dayTempCorridorMax = 0.0;
            _dayHumidityCorridorMin = 0.0;
            _dayHumidityCorridorMax = 0.0;
        }

        public void RecordAlert()
        {
            TotalAlertsCount++;
        }
    }
}
