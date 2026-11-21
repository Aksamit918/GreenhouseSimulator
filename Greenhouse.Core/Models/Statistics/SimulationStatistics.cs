using GreenhouseSimulator.Core.Models.Actuators;
using GreenhouseSimulator.Core.Models.Environment;
using GreenhouseSimulator.Core.Models.Plans;
using System;
using System.Collections.Generic;
using System.Linq;

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

        public double TemperatureSlaPercent => Percent(_tempOkTicks);
        public double HumiditySlaPercent => Percent(_humidityOkTicks);
        public double PhSlaPercent => Percent(_phOkTicks);
        public double LightSlaPercent => Percent(_lightOkTicks);

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

        public void RecordTick(
            int day,
            int hourOfDay,
            double temperature,
            double humidity,
            double ph,
            double light,
            ParameterCorridor temperatureCorridor,
            ParameterCorridor humidityCorridor,
            ParameterCorridor phCorridor,
            bool lightScheduled)
        {
            ArgumentNullException.ThrowIfNull(temperatureCorridor);
            ArgumentNullException.ThrowIfNull(humidityCorridor);
            ArgumentNullException.ThrowIfNull(phCorridor);

            if (_currentDay != 0 && _currentDay != day)
            {
                CloseDay();
            }

            if (_currentDay != day)
            {
                _currentDay = day;
                _hoursInCurrentDay = 0;
                _dayMinTemp = temperature;
                _dayMaxTemp = temperature;
                _daySumTemp = 0.0;
                _dayMinHum = humidity;
                _dayMaxHum = humidity;
                _daySumHum = 0.0;
                _dayTempCorridorMin = temperatureCorridor.MinValue;
                _dayTempCorridorMax = temperatureCorridor.MaxValue;
                _dayHumidityCorridorMin = humidityCorridor.MinValue;
                _dayHumidityCorridorMax = humidityCorridor.MaxValue;
            }

            TotalTicks++;
            if (temperatureCorridor.IsWithinCorridor(temperature))
            {
                _tempOkTicks++;
            }

            if (humidityCorridor.IsWithinCorridor(humidity))
            {
                _humidityOkTicks++;
            }

            if (phCorridor.IsWithinCorridor(ph))
            {
                _phOkTicks++;
            }

            bool lightOk = lightScheduled ? light > 0.0 : light <= 0.0;
            if (lightOk)
            {
                _lightOkTicks++;
            }

            _hoursInCurrentDay++;
            _dayMinTemp = Math.Min(_dayMinTemp, temperature);
            _dayMaxTemp = Math.Max(_dayMaxTemp, temperature);
            _daySumTemp += temperature;
            _dayMinHum = Math.Min(_dayMinHum, humidity);
            _dayMaxHum = Math.Max(_dayMaxHum, humidity);
            _daySumHum += humidity;
            _dayTempCorridorMin = Math.Min(_dayTempCorridorMin, temperatureCorridor.MinValue);
            _dayTempCorridorMax = Math.Max(_dayTempCorridorMax, temperatureCorridor.MaxValue);
            _dayHumidityCorridorMin = Math.Min(_dayHumidityCorridorMin, humidityCorridor.MinValue);
            _dayHumidityCorridorMax = Math.Max(_dayHumidityCorridorMax, humidityCorridor.MaxValue);

            if (hourOfDay == 23)
            {
                CloseDay();
            }
        }

        public void FlushOpenDay()
        {
            CloseDay();
        }

        public IReadOnlyList<string> DescribeZones(
            EnvironmentGrid grid,
            IReadOnlyList<Actuator> actuators,
            ParameterCorridor temperature,
            ParameterCorridor humidity)
        {
            ArgumentNullException.ThrowIfNull(grid);
            ArgumentNullException.ThrowIfNull(actuators);
            ArgumentNullException.ThrowIfNull(temperature);
            ArgumentNullException.ThrowIfNull(humidity);

            ClimateCell coldest = grid.GetCell(0, 0);
            ClimateCell hottest = coldest;
            ClimateCell driest = coldest;
            (double X, double Y) coldAt = grid.GetCellCenter(0, 0);
            (double X, double Y) hotAt = coldAt;
            (double X, double Y) dryAt = coldAt;

            for (int i = 0; i < grid.WidthCells; i++)
            {
                for (int j = 0; j < grid.LengthCells; j++)
                {
                    ClimateCell cell = grid.GetCell(i, j);
                    (double X, double Y) at = grid.GetCellCenter(i, j);
                    if (cell.Temperature < coldest.Temperature)
                    {
                        coldest = cell;
                        coldAt = at;
                    }

                    if (cell.Temperature > hottest.Temperature)
                    {
                        hottest = cell;
                        hotAt = at;
                    }

                    if (cell.RelativeHumidity < driest.RelativeHumidity)
                    {
                        driest = cell;
                        dryAt = at;
                    }
                }
            }

            var lines = new List<string>();
            if (coldest.Temperature < temperature.MinValue - 0.3)
            {
                lines.Add(Advise(grid, actuators.OfType<Heater>(), coldAt, coldest.Temperature, "холодная", "обогреватель", "°C"));
            }

            if (hottest.Temperature > temperature.MaxValue + 0.3)
            {
                lines.Add(Advise(grid, actuators.OfType<AirConditioner>(), hotAt, hottest.Temperature, "горячая", "кондиционер", "°C"));
            }

            if (driest.RelativeHumidity < humidity.MinValue - 1.0)
            {
                lines.Add(Advise(grid, actuators.OfType<Humidifier>(), dryAt, driest.RelativeHumidity, "сухая", "увлажнитель", "%"));
            }

            if (lines.Count == 0)
            {
                lines.Add("Явных холодных, горячих и сухих зон нет.");
            }

            return lines;
        }

        private void CloseDay()
        {
            if (_hoursInCurrentDay <= 0)
            {
                return;
            }

            _dailyHistory.Add(new DailyClimateRecord(
                _currentDay,
                _dayMinTemp,
                _daySumTemp / _hoursInCurrentDay,
                _dayMaxTemp,
                _dayTempCorridorMin,
                _dayTempCorridorMax,
                _dayMinHum,
                _daySumHum / _hoursInCurrentDay,
                _dayMaxHum,
                _dayHumidityCorridorMin,
                _dayHumidityCorridorMax));
            _hoursInCurrentDay = 0;
        }

        private double Percent(int okTicks)
        {
            return TotalTicks > 0 ? okTicks * 100.0 / TotalTicks : 0.0;
        }

        private static string Advise<TActuator>(
            EnvironmentGrid grid,
            IEnumerable<TActuator> actuators,
            (double X, double Y) spot,
            double value,
            string kind,
            string deviceName,
            string unit)
            where TActuator : Actuator
        {
            string place = DescribePlace(spot, grid.WidthCells, grid.LengthCells, grid);
            TActuator? nearest = null;
            double bestDistance = double.MaxValue;
            foreach (TActuator actuator in actuators)
            {
                double dx = actuator.Location.X - spot.X;
                double dy = actuator.Location.Y - spot.Y;
                double distance = dx * dx + dy * dy;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = actuator;
                }
            }

            if (nearest == null)
            {
                return $"В {place} {kind} зона ({value:F1} {unit}). {DeviceTitle(deviceName)} на схеме нет.";
            }

            return $"В {place} {kind} зона ({value:F1} {unit}). Сместите {deviceName} ({nearest.Location.X:F1}; {nearest.Location.Y:F1}) ближе к ({spot.X:F1}; {spot.Y:F1}).";
        }

        private static string DeviceTitle(string deviceName)
        {
            return deviceName switch
            {
                "обогреватель" => "Обогревателя",
                "кондиционер" => "Кондиционера",
                _ => "Увлажнителя"
            };
        }

        private static string DescribePlace((double X, double Y) spot, int widthCells, int lengthCells, EnvironmentGrid grid)
        {
            (double X, double Y) center = grid.GetCellCenter(widthCells / 2, lengthCells / 2);
            bool north = spot.Y >= center.Y;
            bool west = spot.X < center.X;
            if (north && west)
            {
                return "северо-западной зоне";
            }

            if (north)
            {
                return "северо-восточной зоне";
            }

            if (west)
            {
                return "юго-западной зоне";
            }

            return "юго-восточной зоне";
        }
    }
}
