using GreenhouseSimulator.Core.Config;
using GreenhouseSimulator.Core.Models.Environment;
using GreenhouseSimulator.Core.Models.Plans;
using System.Linq;

namespace GreenhouseSimulator.Core.Models.Biology
{
    public readonly record struct DeathGroup(string Cause, int Day, double X, double Y, int CellCount);

    public class CropGrowthModel
    {
        private readonly SimulationConfig _config;
        private readonly double[,] _cellYield;
        private readonly bool[,] _cellDead;
        private readonly List<(string Cause, int Day, double X, double Y)> _deaths = new();
        private readonly int _widthCells;
        private readonly int _lengthCells;
        private int _plannedHours = 24;
        private double _uniformityPenalty;

        public double AccumulatedGdd { get; private set; }
        public double AccumulatedDli { get; private set; }
        public double ColdStress { get; private set; }
        public double HeatStress { get; private set; }
        public double HumidityStress { get; private set; }
        public double PhStress { get; private set; }
        public double TotalStressIndex => ColdStress + HeatStress + HumidityStress + PhStress;
        public int TotalCellCount => _widthCells * _lengthCells;
        public int DeadCellCount => _deaths.Count;
        public bool AllCellsDead => DeadCellCount == TotalCellCount;
        public IReadOnlyList<DeathGroup> DeathGroups
        {
            get
            {
                return _deaths
                    .GroupBy(death => (death.Cause, death.Day))
                    .Select(group => new DeathGroup(
                        group.Key.Cause,
                        group.Key.Day,
                        group.Average(death => death.X),
                        group.Average(death => death.Y),
                        group.Count()))
                    .OrderBy(group => group.Day)
                    .ThenBy(group => group.Cause, StringComparer.Ordinal)
                    .ToList();
            }
        }
        public double YieldPotential
        {
            get
            {
                double sum = 0.0;
                for (int i = 0; i < _widthCells; i++)
                {
                    for (int j = 0; j < _lengthCells; j++)
                    {
                        sum += _cellYield[i, j];
                    }
                }

                return sum / (_widthCells * _lengthCells);
            }
        }
        public double UniformityIndex => Math.Clamp(100.0 - _config.UniformityPenaltyFactor * _uniformityPenalty, 0.0, 100.0);

        public CropGrowthModel(SimulationConfig config, int widthCells, int lengthCells)
        {
            ArgumentNullException.ThrowIfNull(config);
            if (widthCells <= 0 || lengthCells <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(widthCells), "Grid size must be positive.");
            }

            _config = config;
            _widthCells = widthCells;
            _lengthCells = lengthCells;
            _cellYield = new double[widthCells, lengthCells];
            _cellDead = new bool[widthCells, lengthCells];
            Reset();
        }

        public double GetCellYield(int i, int j) => _cellYield[i, j];

        public void SetPlannedDuration(int totalDays)
        {
            _plannedHours = Math.Max(1, totalDays * 24);
        }

        public void Reset()
        {
            for (int i = 0; i < _widthCells; i++)
            {
                for (int j = 0; j < _lengthCells; j++)
                {
                    _cellYield[i, j] = 0.0;
                    _cellDead[i, j] = false;
                }
            }

            _deaths.Clear();
            AccumulatedGdd = 0.0;
            AccumulatedDli = 0.0;
            ColdStress = 0.0;
            HeatStress = 0.0;
            HumidityStress = 0.0;
            PhStress = 0.0;
            _uniformityPenalty = 0.0;
        }

        public void Advance(EnvironmentGrid grid, PlanPhase phase, int hourOfDay, int currentDay, double dt)
        {
            ArgumentNullException.ThrowIfNull(grid);
            ArgumentNullException.ThrowIfNull(phase);

            RememberClimate(grid);

            ParameterCorridor tempCorridor = phase.GetTempCorridor(hourOfDay);
            int totalCells = _widthCells * _lengthCells;
            double span = Math.Max(0.1, tempCorridor.TargetValue - _config.BaseGrowthTemp);

            for (int i = 0; i < _widthCells; i++)
            {
                for (int j = 0; j < _lengthCells; j++)
                {
                    if (_cellDead[i, j])
                    {
                        continue;
                    }

                    ClimateCell cell = grid.GetCell(i, j);
                    if (TryMarkDead(grid, cell, i, j, currentDay))
                    {
                        continue;
                    }

                    if (IsNormal(cell, tempCorridor, phase.HumidityCorridor, phase.PhCorridor))
                    {
                        if (cell.Temperature > _config.BaseGrowthTemp)
                        {
                            double gdd = (cell.Temperature - _config.BaseGrowthTemp) * dt / 24.0;
                            AccumulatedGdd += gdd / totalCells;
                            double growth = 100.0 * ((cell.Temperature - _config.BaseGrowthTemp) / span) * dt / _plannedHours;
                            _cellYield[i, j] = Math.Min(100.0, _cellYield[i, j] + growth);
                        }

                        AccumulatedDli += cell.Light * dt / totalCells;
                        continue;
                    }

                    if (_cellYield[i, j] <= 0.0)
                    {
                        continue;
                    }

                    ApplyCorridorStress(i, j, cell.Temperature, tempCorridor, _config.TempStressFactor, dt, totalCells, isTemperature: true);
                    ApplyCorridorStress(i, j, cell.RelativeHumidity, phase.HumidityCorridor, _config.HumidityStressFactor, dt, totalCells, isTemperature: false, isPh: false);
                    ApplyCorridorStress(i, j, cell.Ph, phase.PhCorridor, _config.PhStressFactor, dt, totalCells, isTemperature: false, isPh: true);
                }
            }
        }

        private void RememberClimate(EnvironmentGrid grid)
        {
            _uniformityPenalty =
                grid.GetTemperatureStdDev() * 5.0
                + grid.GetHumidityStdDev() * 0.4
                + grid.GetPhStdDev() * 8.0
                + grid.GetLightStdDev() * 0.015;
        }

        private static bool IsNormal(ClimateCell cell, ParameterCorridor temperature, ParameterCorridor humidity, ParameterCorridor ph)
        {
            return temperature.IsWithinCorridor(cell.Temperature)
                && humidity.IsWithinCorridor(cell.RelativeHumidity)
                && ph.IsWithinCorridor(cell.Ph);
        }

        private bool TryMarkDead(EnvironmentGrid grid, ClimateCell cell, int i, int j, int currentDay)
        {
            string? cause = null;
            if (cell.Temperature <= _config.LethalMinTemp)
            {
                cause = "заморозки";
            }
            else if (cell.Temperature >= _config.LethalMaxTemp)
            {
                cause = "термический шок";
            }
            else if (cell.Ph <= _config.LethalMinPh || cell.Ph >= _config.LethalMaxPh)
            {
                cause = "химический ожог корней";
            }

            if (cause == null)
            {
                return false;
            }

            (double X, double Y) position = grid.GetCellCenter(i, j);
            _cellDead[i, j] = true;
            _cellYield[i, j] = 0.0;
            _deaths.Add((cause, currentDay, position.X, position.Y));
            return true;
        }

        private void ApplyCorridorStress(
            int i,
            int j,
            double actual,
            ParameterCorridor corridor,
            double factor,
            double dt,
            int totalCells,
            bool isTemperature,
            bool isPh = false)
        {
            double excess;
            bool belowMinimum;
            if (actual < corridor.MinValue)
            {
                excess = corridor.MinValue - actual;
                belowMinimum = true;
            }
            else if (actual > corridor.MaxValue)
            {
                excess = actual - corridor.MaxValue;
                belowMinimum = false;
            }
            else
            {
                return;
            }

            if (_cellYield[i, j] <= 0.0)
            {
                return;
            }

            double penalty = factor * excess * excess * dt;
            double applied = Math.Min(penalty, _cellYield[i, j]);
            _cellYield[i, j] -= applied;
            double normalized = applied / totalCells;

            if (isTemperature)
            {
                if (belowMinimum)
                {
                    ColdStress += normalized;
                }
                else
                {
                    HeatStress += normalized;
                }

                return;
            }

            if (isPh)
            {
                PhStress += normalized;
                return;
            }

            HumidityStress += normalized;
        }
    }
}
