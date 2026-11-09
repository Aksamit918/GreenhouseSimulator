using GreenhouseSimulator.Core.Config;
using GreenhouseSimulator.Core.Models.Environment;
using GreenhouseSimulator.Core.Models.Plans;

namespace GreenhouseSimulator.Core.Models.Biology
{
    public class CropGrowthModel
    {
        private readonly SimulationConfig _config;
        private readonly double[,] _cellYield;
        private readonly int _widthCells;
        private readonly int _lengthCells;

        public double AccumulatedGdd { get; private set; }
        public double AccumulatedDli { get; private set; }
        public double ColdStress { get; private set; }
        public double HeatStress { get; private set; }
        public double HumidityStress { get; private set; }
        public double PhStress { get; private set; }
        public double TotalStressIndex => ColdStress + HeatStress + HumidityStress + PhStress;
        public bool IsLethal { get; private set; }
        public string? LethalReason { get; private set; }

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
            Reset();
        }

        public double GetCellYield(int i, int j) => _cellYield[i, j];

        public void Reset()
        {
            for (int i = 0; i < _widthCells; i++)
            {
                for (int j = 0; j < _lengthCells; j++)
                {
                    _cellYield[i, j] = 100.0;
                }
            }

            AccumulatedGdd = 0.0;
            AccumulatedDli = 0.0;
            ColdStress = 0.0;
            HeatStress = 0.0;
            HumidityStress = 0.0;
            PhStress = 0.0;
            IsLethal = false;
            LethalReason = null;
        }

        public void Advance(EnvironmentGrid grid, PlanPhase phase, int hourOfDay, int currentDay, double dt)
        {
            ArgumentNullException.ThrowIfNull(grid);
            ArgumentNullException.ThrowIfNull(phase);
            if (IsLethal)
            {
                return;
            }

            double averageTemperature = grid.GetAverageTemperature();
            AccumulatedGdd += Math.Max(0.0, averageTemperature - _config.BaseGrowthTemp) * dt / 24.0;
            AccumulatedDli += grid.GetAverageLight() * dt;

            ParameterCorridor tempCorridor = phase.GetTempCorridor(hourOfDay);
            int totalCells = _widthCells * _lengthCells;

            for (int i = 0; i < _widthCells; i++)
            {
                for (int j = 0; j < _lengthCells; j++)
                {
                    ClimateCell cell = grid.GetCell(i, j);
                    if (TryMarkLethal(grid, cell, i, j, currentDay))
                    {
                        return;
                    }

                    ApplyCorridorStress(i, j, cell.Temperature, tempCorridor, _config.TempStressFactor, dt, totalCells, isTemperature: true);
                    ApplyCorridorStress(i, j, cell.RelativeHumidity, phase.HumidityCorridor, _config.HumidityStressFactor, dt, totalCells, isTemperature: false, isPh: false);
                    ApplyCorridorStress(i, j, cell.Ph, phase.PhCorridor, _config.PhStressFactor, dt, totalCells, isTemperature: false, isPh: true);
                }
            }
        }

        private bool TryMarkLethal(EnvironmentGrid grid, ClimateCell cell, int i, int j, int currentDay)
        {
            (double X, double Y) position = grid.GetCellCenter(i, j);
            string zone = $"({position.X:F1}; {position.Y:F1}) м";

            if (cell.Temperature <= _config.LethalMinTemp)
            {
                MarkDead($"Гибель от заморозков на {currentDay}-е сутки в зоне {zone} (T = {cell.Temperature:F1} °C)");
                return true;
            }

            if (cell.Temperature >= _config.LethalMaxTemp)
            {
                MarkDead($"Термический шок на {currentDay}-е сутки в зоне {zone} (T = {cell.Temperature:F1} °C)");
                return true;
            }

            if (cell.Ph <= _config.LethalMinPh || cell.Ph >= _config.LethalMaxPh)
            {
                MarkDead($"Химический ожог корней на {currentDay}-е сутки в зоне {zone} (pH = {cell.Ph:F1})");
                return true;
            }

            return false;
        }

        private void MarkDead(string reason)
        {
            IsLethal = true;
            LethalReason = reason;
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

            double penalty = factor * excess * excess * dt;
            _cellYield[i, j] = Math.Max(0.0, _cellYield[i, j] - penalty);
            double normalized = penalty / totalCells;

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
