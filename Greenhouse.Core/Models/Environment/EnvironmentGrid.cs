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

        public EnvironmentGrid(SimulationConfig config)
        {
            _config = config;
            WidthCells = (int)Math.Ceiling(config.Width / config.GridStep);
            LengthCells = (int)Math.Ceiling(config.Length / config.GridStep);
            _currentCells = new ClimateCell[WidthCells, LengthCells];
            _nextCells = new ClimateCell[WidthCells, LengthCells];

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
        }

        public (double X, double Y) GetCellCenter(int i, int j)
        {
            double x = (i + 0.5) * _config.GridStep;
            double y = (j + 0.5) * _config.GridStep;

            return (x, y);
        }

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
            GetInterpolatedValue(location, cell => cell.Ph);

        public double GetOutsideTemperature(double hourOfDay)
        {
            double t_avg = (_config.TMax +  _config.TMin) / 2;
            double A = (_config.TMax - _config.TMin) / 2;
            double h_min = _config.OutsideMinTempHour;

            return t_avg - A * Math.Cos((hourOfDay - h_min) * (Math.PI / 12.0));
        }

        public void ApplyWallHeatLoss(double outsideTemp, double dt)
        {
            for (int i = 0; i < WidthCells; i++)
            {
                var cell1 = _currentCells[i, 0];
                cell1.Temperature -= _config.WallInsulationCoeff * (cell1.Temperature - outsideTemp) * dt;

                var cell2 = _currentCells[i, LengthCells - 1];
                cell2.Temperature -= _config.WallInsulationCoeff * (cell2.Temperature - outsideTemp) * dt;
            }

            for (int j = 0; j < LengthCells; j++)
            {
                var cell1 = _currentCells[0, j];
                cell1.Temperature -= _config.WallInsulationCoeff * (cell1.Temperature - outsideTemp) * dt;

                var cell2 = _currentCells[WidthCells - 1, j];
                cell2.Temperature -= _config.WallInsulationCoeff * (cell2.Temperature - outsideTemp) * dt;
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

        public void ApplyActuators(IReadOnlyList<Actuator> actuators, double dt)
        {
            foreach (var actuator in actuators)
                actuator.UpdateInertia(dt);

            for (int i = 0; i < WidthCells; i++)
            {
                for (int j = 0; j < LengthCells; j++)
                {
                    _currentCells[i, j].Light = _config.InitialLight;

                    var center = GetCellCenter(i, j);
                    ClimateDelta totalDelta = ClimateDelta.Zero;
                    foreach (var actuator in actuators)
                        totalDelta += actuator.GetImpactAt(center, dt, _config.UseGaussianAttenuation);

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
                    cell.Ph -= _config.PhDriftRate * dt;
                }
            }
        }

        public void ApplyHydroponicCirculation(double dt)
        {
            double effectivePhD = Math.Min(_config.PhDiffusionCoeff * dt, _config.MaxDiffusionStepFactor);
            double effectiveFlow = Math.Min(_config.SolutionFlowSpeed * dt, _config.MaxDiffusionStepFactor);

            for (int i = 0; i < WidthCells; i++)
            {
                for (int j = 0; j < LengthCells; j++)
                {
                    _nextCells[i, j].CopyFrom(_currentCells[i, j]);

                    int count = 0;
                    double phNeighbours = 0.0;

                    if (i > 0)
                    {
                        phNeighbours += _currentCells[i - 1, j].Ph;
                        count++;
                    }
                    if (i < WidthCells - 1)
                    {
                        phNeighbours += _currentCells[i + 1, j].Ph;
                        count++;
                    }
                    if (j > 0)
                    {
                        phNeighbours += _currentCells[i, j - 1].Ph;
                        count++;
                    }
                    if (j < LengthCells - 1)
                    {
                        phNeighbours += _currentCells[i, j + 1].Ph;
                        count++;
                    }

                    int upstreamI = (i > 0) ? i - 1 : WidthCells - 1;

                    double currentPh = _currentCells[i, j].Ph;
                    double diffusionDelta = effectivePhD * (phNeighbours / count - currentPh);
                    double flowDelta = effectiveFlow * (_currentCells[upstreamI, j].Ph - currentPh);

                    _nextCells[i, j].Ph = currentPh + diffusionDelta + flowDelta;
                }
            }

            (_currentCells, _nextCells) = (_nextCells, _currentCells);
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

        public void StepPhysics(double hourOfDay, IReadOnlyList<Actuator> actuators)
        {
            double dt = _config.HoursPerTick / _config.SubstepsPerHour;
            double outsideTemp = 0.0;
            for (int i = 0; i < _config.SubstepsPerHour; i++)
            {
                outsideTemp = GetOutsideTemperature(hourOfDay + i * dt);
                ApplyWallHeatLoss(outsideTemp, dt);
                ApplyActuators(actuators, dt);
                ApplyDiffusion(dt);
                ApplyHydroponicCirculation(dt);
                ApplyPlantMetabolism(dt);
                EnforceLimits();
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
    }
}
