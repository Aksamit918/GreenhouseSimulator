using GreenhouseSimulator.Core.Config;
using GreenhouseSimulator.Core.Models.Actuators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GreenhouseSimulator.Core.Models.Environment
{
    public class EnvironmentGrid
    {
        private readonly SimulationConfig _config;
        public int WidthCells { get; }
        public int LengthCells { get; }
        private ClimateCell[,] _currentCells;
        private ClimateCell[,] _nextCells;
        private readonly SolutionCircuit _solution;

        public EnvironmentGrid(SimulationConfig config)
        {
            _config = config;
            WidthCells = (int)Math.Ceiling(config.Width / config.GridStep);
            LengthCells = (int)Math.Ceiling(config.Length / config.GridStep);
            _currentCells = new ClimateCell[WidthCells, LengthCells];
            _nextCells = new ClimateCell[WidthCells, LengthCells];
            _solution = new SolutionCircuit(config, WidthCells);

            for (int i = 0; i < WidthCells; i++)
            {
                for (int j = 0; j < LengthCells; j++)
                {
                    _currentCells[i, j] = new ClimateCell
                    {
                        Temperature = config.InitialTemperature,
                        AbsoluteHumidity = config.InitialAbsoluteHumidity,
                        Light = config.InitialLight,
                        Ph = config.InitialPh
                    };
                    _nextCells[i, j] = new ClimateCell();
                    _nextCells[i, j].CopyFrom(_currentCells[i, j]);
                }
            }

            PublishSolution();
        }

        public void Reset()
        {
            _solution.Reset();

            for (int i = 0; i < WidthCells; i++)
            {
                for (int j = 0; j < LengthCells; j++)
                {
                    _currentCells[i, j].Temperature = _config.InitialTemperature;
                    _currentCells[i, j].AbsoluteHumidity = _config.InitialAbsoluteHumidity;
                    _currentCells[i, j].Light = _config.InitialLight;
                    _currentCells[i, j].Ph = _config.InitialPh;
                    _currentCells[i, j].Ec = _config.InitialEc;
                    _nextCells[i, j].CopyFrom(_currentCells[i, j]);
                }
            }

            PublishSolution();
        }

        public (double X, double Y) GetCellCenter(int i, int j)
        {
            double x = (i + 0.5) * _config.GridStep;
            double y = (j + 0.5) * _config.GridStep;

            return (x, y);
        }

        public ClimateCell GetCell(int i, int j) => _currentCells[i, j];

        public (int i, int j) WorldToGridIndex((double X, double Y) coordinates)
        {
            int i = Math.Clamp((int)(coordinates.X / _config.GridStep), 0, WidthCells - 1);
            int j = Math.Clamp((int)(coordinates.Y / _config.GridStep), 0, LengthCells - 1);

            return (i, j);
        }

        private double GetInterpolatedValue((double X, double Y) location, Func<ClimateCell, double> selector)
        {
            double gx = (location.X / _config.GridStep) - 0.5;
            double gy = (location.Y / _config.GridStep) - 0.5;

            int i0 = (int)Math.Floor(gx);
            int j0 = (int)Math.Floor(gy);
            int i1 = i0 + 1;
            int j1 = j0 + 1;

            double tx = Math.Clamp(gx - i0, 0.0, 1.0);
            double ty = Math.Clamp(gy - j0, 0.0, 1.0);

            i0 = Math.Clamp(i0, 0, WidthCells - 1);
            i1 = Math.Clamp(i1, 0, WidthCells - 1);
            j0 = Math.Clamp(j0, 0, LengthCells - 1);
            j1 = Math.Clamp(j1, 0, LengthCells - 1);

            double v00 = selector(_currentCells[i0, j0]);
            double v10 = selector(_currentCells[i1, j0]);
            double v01 = selector(_currentCells[i0, j1]);
            double v11 = selector(_currentCells[i1, j1]);

            double top = v00 * (1 - tx) + v10 * tx;
            double bottom = v01 * (1 - tx) + v11 * tx;

            return top * (1 - ty) + bottom * ty;
        }

        public double GetTemperatureAt((double X, double Y) location) =>
            GetInterpolatedValue(location, cell => cell.Temperature);

        public double GetHumidityAt((double X, double Y) location) =>
            GetInterpolatedValue(location, cell => cell.RelativeHumidity);

        public double GetLightAt((double X, double Y) location) =>
            GetInterpolatedValue(location, cell => cell.Light);

        public double GetPhAt((double X, double Y) location) =>
            _solution.PhAt(location.X, location.Y);

        public double GetEcAt((double X, double Y) location) =>
            _solution.EcAt(location.X, location.Y);

        public double GetOutsideTemperature(double hourOfDay)
        {
            double t_avg = (_config.TMax +  _config.TMin) / 2;
            double A = (_config.TMax - _config.TMin) / 2;
            double h_min = _config.OutsideMinTempHour;

            return t_avg - A * Math.Cos((hourOfDay - h_min) * (Math.PI / 12.0));
        }

        public void ApplyWallHeatLoss(double outsideTemp, double dt)
        {
            void Cool(ClimateCell cell)
            {
                cell.Temperature -= _config.WallInsulationCoeff * (cell.Temperature - outsideTemp) * dt;
            }

            for (int i = 0; i < WidthCells; i++)
            {
                Cool(_currentCells[i, 0]);
                if (LengthCells > 1)
                {
                    Cool(_currentCells[i, LengthCells - 1]);
                }
            }

            for (int j = 1; j < LengthCells - 1; j++)
            {
                Cool(_currentCells[0, j]);
                if (WidthCells > 1)
                {
                    Cool(_currentCells[WidthCells - 1, j]);
                }
            }
        }

        public void ApplyDiffusion(double dt)
        {
            double effectiveD = Math.Min(_config.DiffusionCoeff * dt, _config.MaxDiffusionStepFactor);

            for (int i = 0; i < WidthCells; i++)
            {
                for (int j = 0; j < LengthCells; j++)
                {
                    _nextCells[i, j].CopyFrom(_currentCells[i, j]);

                    double tNeighbours = 0.0;
                    double hNeighbours = 0.0;
                    int count = 0;

                    if (i > 0)
                    {
                        tNeighbours += _currentCells[i - 1, j].Temperature;
                        hNeighbours += _currentCells[i - 1, j].AbsoluteHumidity;
                        count++;
                    }
                    if (i < WidthCells - 1)
                    {
                        tNeighbours += _currentCells[i + 1, j].Temperature;
                        hNeighbours += _currentCells[i + 1, j].AbsoluteHumidity;
                        count++;
                    }
                    if (j > 0)
                    {
                        tNeighbours += _currentCells[i, j - 1].Temperature;
                        hNeighbours += _currentCells[i, j - 1].AbsoluteHumidity;
                        count++;
                    }
                    if (j < LengthCells - 1)
                    {
                        tNeighbours += _currentCells[i, j + 1].Temperature;
                        hNeighbours += _currentCells[i, j + 1].AbsoluteHumidity;
                        count++;
                    }

                    double currentTemperature = _currentCells[i, j].Temperature;
                    double newT = currentTemperature + effectiveD * (tNeighbours / count - currentTemperature);

                    double currentHumidity = _currentCells[i, j].AbsoluteHumidity;
                    double newH = currentHumidity + effectiveD * (hNeighbours / count - currentHumidity);


                    _nextCells[i, j].Temperature = newT;
                    _nextCells[i, j].AbsoluteHumidity = newH;
                }
            }

            (_currentCells, _nextCells) = (_nextCells, _currentCells);
        }

        public void ApplyAirCirculation()
        {
            double temperatureSum = 0.0;
            double humiditySum = 0.0;
            int count = WidthCells * LengthCells;
            for (int i = 0; i < WidthCells; i++)
            {
                for (int j = 0; j < LengthCells; j++)
                {
                    temperatureSum += _currentCells[i, j].Temperature;
                    humiditySum += _currentCells[i, j].AbsoluteHumidity;
                }
            }

            double meanTemperature = temperatureSum / count;
            double meanHumidity = humiditySum / count;
            for (int i = 0; i < WidthCells; i++)
            {
                for (int j = 0; j < LengthCells; j++)
                {
                    bool perimeter = i == 0 || j == 0 || i == WidthCells - 1 || j == LengthCells - 1;
                    double mix = perimeter ? _config.PerimeterAirMix : _config.InteriorAirMix;
                    ClimateCell cell = _currentCells[i, j];
                    cell.Temperature += mix * (meanTemperature - cell.Temperature);
                    cell.AbsoluteHumidity += mix * (meanHumidity - cell.AbsoluteHumidity);
                }
            }
        }

        public void ApplyActuators(IReadOnlyList<Actuator> actuators, double dt)
        {
            foreach (var actuator in actuators)
            {
                actuator.UpdateInertia(dt);
            }

            for (int i = 0; i < WidthCells; i++)
            {
                for (int j = 0; j < LengthCells; j++)
                {
                    _currentCells[i, j].Light = _config.InitialLight;

                    var center = GetCellCenter(i, j);
                    ClimateDelta totalDelta = ClimateDelta.Zero;
                    foreach (var actuator in actuators)
                    {
                        totalDelta += actuator.GetImpactAt(center, dt, _config.UseGaussianAttenuation);
                    }

                    _currentCells[i, j].ApplyDelta(totalDelta);
                }
            }
        }

        public void ApplyPlantMetabolism(double dt)
        {
            for (int i = 0; i < WidthCells; i++)
            {
                for (int j = 0; j < LengthCells; j++)
                {
                    var cell = _currentCells[i, j];

                    double deltaAh = (_config.BaseTranspirationRate + _config.LightTranspirationFactor * cell.Light) * dt;
                    cell.AbsoluteHumidity += deltaAh;
                }
            }
        }

        public void ApplyHydroponicCirculation(double dt, IReadOnlyList<Actuator> actuators)
        {
            _solution.Advance(dt, actuators);
            PublishSolution();
        }

        private void PublishSolution()
        {
            for (int i = 0; i < WidthCells; i++)
            {
                for (int j = 0; j < LengthCells; j++)
                {
                    (double X, double Y) center = GetCellCenter(i, j);
                    _currentCells[i, j].Ph = _solution.PhAt(center.X, center.Y);
                    _currentCells[i, j].Ec = _solution.EcAt(center.X, center.Y);
                    _nextCells[i, j].Ph = _currentCells[i, j].Ph;
                    _nextCells[i, j].Ec = _currentCells[i, j].Ec;
                }
            }
        }

        private void EnforceLimits()
        {
            for (int i = 0; i < WidthCells; i++)
            {
                for (int j = 0; j < LengthCells; j++)
                {
                    _currentCells[i, j].EnforcePhysicalLimits(_config.MinPh, _config.MaxPh);
                }
            }
        }

        public void ApplySolar(double hourOfDay, double dt)
        {
            double elevation = SolarElevation(hourOfDay);
            if (elevation <= 0.0)
            {
                return;
            }

            double heat = _config.SolarPeakHeat * elevation * dt;
            double light = _config.SolarPeakLight * elevation;
            for (int i = 0; i < WidthCells; i++)
            {
                for (int j = 0; j < LengthCells; j++)
                {
                    _currentCells[i, j].Temperature += heat;
                    _currentCells[i, j].Light += light;
                }
            }
        }

        private double SolarElevation(double hourOfDay)
        {
            if (_config.SolarDayLengthHours <= 0.0)
            {
                return 0.0;
            }

            double sunrise = _config.SolarNoonHour - _config.SolarDayLengthHours / 2.0;
            double position = (hourOfDay - sunrise) / _config.SolarDayLengthHours;
            if (position <= 0.0 || position >= 1.0)
            {
                return 0.0;
            }

            return Math.Sin(Math.PI * position);
        }

        public void StepSubstep(double hourOfDay, double dt, IReadOnlyList<Actuator> actuators)
        {
            ApplyWallHeatLoss(GetOutsideTemperature(hourOfDay), dt);
            ApplyActuators(actuators, dt);
            ApplySolar(hourOfDay, dt);
            ApplyDiffusion(dt);
            ApplyAirCirculation();
            ApplyHydroponicCirculation(dt, actuators);
            ApplyPlantMetabolism(dt);
            EnforceLimits();
        }

        public void StepPhysics(double hourOfDay, IReadOnlyList<Actuator> actuators)
        {
            double dt = _config.HoursPerTick / _config.SubstepsPerHour;
            for (int i = 0; i < _config.SubstepsPerHour; i++)
            {
                StepSubstep(hourOfDay + i * dt, dt, actuators);
            }
        }

        private double GetAverageValue(Func<ClimateCell, double> selector)
        {
            double sum = 0.0;

            for (int i = 0; i < WidthCells; i++)
            {
                for (int j = 0; j < LengthCells; j++)
                {
                    sum += selector(_currentCells[i, j]);
                }
            }

            return sum / (WidthCells * LengthCells);
        }

        public double GetAverageTemperature() => GetAverageValue(cell => cell.Temperature);
        public double GetAverageHumidity() => GetAverageValue(cell => cell.RelativeHumidity);
        public double GetAveragePh() => GetAverageValue(cell => cell.Ph);
        public double GetAverageLight() => GetAverageValue(cell => cell.Light);


        private double GetStandardDeviation(Func<ClimateCell, double> selector)
        {
            double mean = GetAverageValue(selector);
            double sumSquares = 0.0;

            foreach (ClimateCell cell in _currentCells)
            {
                double diff = selector(cell) - mean;
                sumSquares += diff * diff;
            }

            return Math.Sqrt(sumSquares / _currentCells.Length);
        }
        public double GetTemperatureStdDev() => GetStandardDeviation(cell => cell.Temperature);
        public double GetHumidityStdDev() => GetStandardDeviation(cell => cell.RelativeHumidity);
        public double GetPhStdDev() => GetStandardDeviation(cell => cell.Ph);
        public double GetLightStdDev() => GetStandardDeviation(cell => cell.Light);
    }
}
