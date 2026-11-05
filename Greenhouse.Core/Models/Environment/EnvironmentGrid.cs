using GreenhouseSimulator.Core.Config;
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
                    _currentCells[i, j] = new ClimateCell();
                    _nextCells[i, j] = new ClimateCell();
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
    }
}
