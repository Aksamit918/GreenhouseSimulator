using GreenhouseSimulator.Core.Config;
using GreenhouseSimulator.Core.Models.Environment;
using GreenhouseSimulator.Core.Models.Plans;
using System.Linq;

namespace GreenhouseSimulator.Core.Models.Biology
{
    public readonly record struct DeathGroup(string Cause, int Day, double X, double Y, int CellCount);

    public class CropGrowthModel
    {
        private const double UniformityTempWeight = 5.0;
        private const double UniformityHumidityWeight = 0.4;
        private const double UniformityPhWeight = 8.0;
        private const double UniformityLightWeight = 0.015;

        private readonly SimulationConfig _config;
        private readonly double[,] _cellYield;
        private readonly bool[,] _cellDead;
        private readonly string?[,] _deathCause;
        private readonly List<(string Cause, int Day, double X, double Y)> _deaths = new();
        private readonly int _widthCells;
        private readonly int _lengthCells;
        private int _plannedHours = 24;
        private double _uniformityPenalty;
        private double _coldLoss;
        private double _heatLoss;
        private double _humidityLoss;
        private double _phLoss;
        private double _lightLoss;
        private double _elapsedPotential;

        public double AccumulatedGdd { get; private set; }
        public double AccumulatedDli { get; private set; }
        public double ColdStress => ExplainLosses().Cold;
        public double HeatStress => ExplainLosses().Heat;
        public double HumidityStress => ExplainLosses().Humidity;
        public double PhStress => ExplainLosses().Ph;
        public double LightLoss => ExplainLosses().Light;
        public double UnfinishedPotential => ExplainLosses().Unfinished;
        public double TotalStressIndex => ColdStress + HeatStress + HumidityStress + PhStress + LightLoss;
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
            _deathCause = new string?[widthCells, lengthCells];
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
                    _deathCause[i, j] = null;
                }
            }

            _deaths.Clear();
            AccumulatedGdd = 0.0;
            AccumulatedDli = 0.0;
            _coldLoss = 0.0;
            _heatLoss = 0.0;
            _humidityLoss = 0.0;
            _phLoss = 0.0;
            _lightLoss = 0.0;
            _elapsedPotential = 0.0;
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
            double full = 100.0 * dt / _plannedHours;

            for (int i = 0; i < _widthCells; i++)
            {
                for (int j = 0; j < _lengthCells; j++)
                {
                    _elapsedPotential += full / totalCells;
                    if (_cellDead[i, j])
                    {
                        AddDeathHour(i, j, full / totalCells);
                        continue;
                    }

                    ClimateCell cell = grid.GetCell(i, j);
                    double livingYield = _cellYield[i, j];
                    if (TryMarkDead(grid, cell, i, j, currentDay))
                    {
                        AddDeathHour(i, j, (livingYield + full) / totalCells);
                        continue;
                    }

                    double suitTemperature = Suitability(cell.Temperature, tempCorridor, _config.GrowthMarginTemp);
                    double suitHumidity = Suitability(cell.RelativeHumidity, phase.HumidityCorridor, _config.GrowthMarginHumidity);
                    double suitPh = Suitability(cell.Ph, phase.PhCorridor, _config.GrowthMarginPh);
                    double lightFactor = _config.LightSaturation <= 0.0
                        ? 0.0
                        : Math.Clamp(cell.Light / _config.LightSaturation, 0.0, 1.0);

                    if (suitTemperature >= 1.0 && cell.Temperature > _config.BaseGrowthTemp)
                    {
                        double gdd = (cell.Temperature - _config.BaseGrowthTemp) * dt / 24.0;
                        AccumulatedGdd += gdd / totalCells;
                    }

                    AccumulatedDli += cell.Light * dt / totalCells;

                    double tempRatio = cell.Temperature > _config.BaseGrowthTemp
                        ? (cell.Temperature - _config.BaseGrowthTemp) / span
                        : 0.0;
                    double raw = full * tempRatio * suitTemperature * suitHumidity * suitPh * lightFactor;
                    if (raw > 0.0)
                    {
                        _cellYield[i, j] = Math.Min(100.0, _cellYield[i, j] + raw);
                    }

                    AccountForGrowth(
                        full,
                        raw,
                        cell.Temperature > tempCorridor.MaxValue,
                        tempRatio,
                        suitTemperature,
                        suitHumidity,
                        suitPh,
                        lightFactor,
                        totalCells);

                    if (_cellYield[i, j] <= 0.0)
                    {
                        continue;
                    }

                    ApplyCorridorStress(i, j, cell.Temperature, tempCorridor, _config.GrowthMarginTemp, _config.TempStressFactor, dt, totalCells, isTemperature: true);
                    ApplyCorridorStress(i, j, cell.RelativeHumidity, phase.HumidityCorridor, _config.GrowthMarginHumidity, _config.HumidityStressFactor, dt, totalCells, isTemperature: false, isPh: false);
                    ApplyCorridorStress(i, j, cell.Ph, phase.PhCorridor, _config.GrowthMarginPh, _config.PhStressFactor, dt, totalCells, isTemperature: false, isPh: true);
                }
            }
        }

        private void RememberClimate(EnvironmentGrid grid)
        {
            _uniformityPenalty =
                grid.GetTemperatureStdDev() * UniformityTempWeight
                + grid.GetHumidityStdDev() * UniformityHumidityWeight
                + grid.GetPhStdDev() * UniformityPhWeight
                + grid.GetLightStdDev() * UniformityLightWeight;
        }

        private static double Suitability(double actual, ParameterCorridor corridor, double margin)
        {
            if (corridor.IsWithinCorridor(actual))
            {
                return 1.0;
            }

            double span = Math.Max(0.01, margin);
            double excess = actual < corridor.MinValue
                ? corridor.MinValue - actual
                : actual - corridor.MaxValue;
            return Math.Clamp(1.0 - excess / span, 0.0, 1.0);
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
            _deathCause[i, j] = cause;
            _deaths.Add((cause, currentDay, position.X, position.Y));
            return true;
        }

        private void AccountForGrowth(
            double full,
            double raw,
            bool heat,
            double tempRatio,
            double suitTemperature,
            double suitHumidity,
            double suitPh,
            double lightFactor,
            int totalCells)
        {
            double biological = Math.Max(0.0, full - raw);
            if (biological <= 1e-8)
            {
                return;
            }

            Distribute(biological / totalCells, heat, tempRatio, suitTemperature, suitHumidity, suitPh, lightFactor);
        }

        private void Distribute(
            double amount,
            bool heat,
            double tempRatio,
            double suitTemperature,
            double suitHumidity,
            double suitPh,
            double lightFactor)
        {
            double thermal = Math.Min(1.0, Math.Max(0.0, tempRatio)) * suitTemperature;
            int blocking = 0;
            if (thermal <= 1e-9)
            {
                blocking++;
            }

            if (suitHumidity <= 1e-9)
            {
                blocking++;
            }

            if (suitPh <= 1e-9)
            {
                blocking++;
            }

            if (lightFactor <= 1e-9)
            {
                blocking++;
            }

            if (blocking > 0)
            {
                double share = amount / blocking;
                if (thermal <= 1e-9)
                {
                    AddThermal(share, heat);
                }

                if (suitHumidity <= 1e-9)
                {
                    _humidityLoss += share;
                }

                if (suitPh <= 1e-9)
                {
                    _phLoss += share;
                }

                if (lightFactor <= 1e-9)
                {
                    _lightLoss += share;
                }

                return;
            }

            double thermalWeight = -Math.Log(Math.Min(1.0, thermal));
            double humidityWeight = -Math.Log(Math.Min(1.0, suitHumidity));
            double phWeight = -Math.Log(Math.Min(1.0, suitPh));
            double lightWeight = -Math.Log(Math.Min(1.0, lightFactor));
            double sum = thermalWeight + humidityWeight + phWeight + lightWeight;
            if (sum <= 1e-12)
            {
                AddThermal(amount, heat);
                return;
            }

            AddThermal(amount * thermalWeight / sum, heat);
            _humidityLoss += amount * humidityWeight / sum;
            _phLoss += amount * phWeight / sum;
            _lightLoss += amount * lightWeight / sum;
        }

        private void AddThermal(double amount, bool heat)
        {
            if (heat)
            {
                _heatLoss += amount;
            }
            else
            {
                _coldLoss += amount;
            }
        }

        private void AddDeathHour(int i, int j, double amount)
        {
            switch (_deathCause[i, j])
            {
                case "термический шок":
                    _heatLoss += amount;
                    break;
                case "химический ожог корней":
                    _phLoss += amount;
                    break;
                default:
                    _coldLoss += amount;
                    break;
            }
        }

        private readonly record struct LossBreakdown(
            double Cold,
            double Heat,
            double Humidity,
            double Ph,
            double Light,
            double Unfinished);

        private LossBreakdown ExplainLosses()
        {
            double cold = _coldLoss;
            double heat = _heatLoss;
            double humidity = _humidityLoss;
            double ph = _phLoss;
            double light = _lightLoss;
            double gross = cold + heat + humidity + ph + light;
            double gap = Math.Max(0.0, 100.0 - YieldPotential);
            double behind = Math.Min(gap, Math.Max(0.0, _elapsedPotential - YieldPotential));
            if (behind <= 1e-9 || gross <= 1e-9)
            {
                cold = 0.0;
                heat = 0.0;
                humidity = 0.0;
                ph = 0.0;
                light = 0.0;
                if (behind > 1e-9)
                {
                    cold = behind;
                }
            }
            else
            {
                double scale = behind / gross;
                cold *= scale;
                heat *= scale;
                humidity *= scale;
                ph *= scale;
                light *= scale;
            }

            double unfinished = Math.Max(0.0, gap - (cold + heat + humidity + ph + light));
            return new LossBreakdown(cold, heat, humidity, ph, light, unfinished);
        }

        private void ApplyCorridorStress(
            int i,
            int j,
            double actual,
            ParameterCorridor corridor,
            double margin,
            double factor,
            double dt,
            int totalCells,
            bool isTemperature,
            bool isPh = false)
        {
            double lower = corridor.MinValue - margin;
            double upper = corridor.MaxValue + margin;
            double excess;
            bool belowMinimum;
            if (actual < lower)
            {
                excess = lower - actual;
                belowMinimum = true;
            }
            else if (actual > upper)
            {
                excess = actual - upper;
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
                    _coldLoss += normalized;
                }
                else
                {
                    _heatLoss += normalized;
                }

                return;
            }

            if (isPh)
            {
                _phLoss += normalized;
                return;
            }

            _humidityLoss += normalized;
        }
    }
}
