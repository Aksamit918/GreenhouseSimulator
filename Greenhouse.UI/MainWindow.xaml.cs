using GreenhouseSimulator.Core.Controllers;
using GreenhouseSimulator.Core.Models.Actuators;
using GreenhouseSimulator.Core.Models.Environment;
using GreenhouseSimulator.Core.Models.Plans;
using GreenhouseSimulator.Core.Models.Sensors;
using GreenhouseSimulator.Core.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Greenhouse.UI
{
    public partial class MainWindow : Window
    {
        private GreenhouseController _controller;
        private DispatcherTimer _timer;
        private Dictionary<string, string> _currentAlerts = new Dictionary<string, string>();
        private Dictionary<Actuator, bool> _actuatorStates = new Dictionary<Actuator, bool>();
        private string? _climateRegime;
        private bool _editingExistingPlan;

        private const int PixelsPerCell = 8;
        private const int RampResolution = 512;
        private const double HeatmapBlendMilliseconds = 450.0;

        private WriteableBitmap? _heatmapBitmap;
        private int[] _heatmapPixels = Array.Empty<int>();
        private Image? _heatmapImage;
        private Path? _gridHalo;
        private Path? _gridLine;
        private int _gridCellsX;
        private int _gridCellsY;
        private double _gridFieldWidth;
        private double _gridFieldHeight;

        private int _cellsX;
        private int _cellsY;
        private int _heatmapMode;
        private double[] _shownValues = Array.Empty<double>();
        private double[] _blendFrom = Array.Empty<double>();
        private double[] _blendTo = Array.Empty<double>();
        private int[] _columnLow = Array.Empty<int>();
        private int[] _columnHigh = Array.Empty<int>();
        private double[] _columnWeight = Array.Empty<double>();
        private int[] _rowLow = Array.Empty<int>();
        private int[] _rowHigh = Array.Empty<int>();
        private double[] _rowWeight = Array.Empty<double>();
        private readonly double[] _rampStops = new double[7];
        private readonly double[] _rampFromStops = new double[7];
        private readonly double[] _shownStops = new double[7];
        private int _rampCount;
        private readonly Color[] _rampColors = new Color[7];
        private readonly int[] _rampTable = new int[RampResolution];
        private double _rampMin;
        private double _rampScale;
        private readonly Stopwatch _blendClock = new Stopwatch();
        private bool _blending;
        private readonly double[] _legendStops = new double[7];
        private int _legendMode;
        private int _legendCount;
        private double _legendLow = double.PositiveInfinity;
        private double _legendHigh = double.NegativeInfinity;
        private (int X, int Y)? _probedCell;

        private readonly Dictionary<object, DeviceVisual> _deviceVisuals = new Dictionary<object, DeviceVisual>();
        private static readonly DropShadowEffect DeviceShadow = Frozen(new DropShadowEffect { Color = Colors.Black, Direction = 315, ShadowDepth = 4, BlurRadius = 5, Opacity = 0.6 });
        private static readonly DropShadowEffect DeviceGlow = Frozen(new DropShadowEffect { Color = Colors.Red, Direction = 0, ShadowDepth = 0, BlurRadius = 20, Opacity = 1.0 });

        private sealed class DeviceVisual
        {
            public DeviceVisual(FrameworkElement root, double halfWidth, double halfHeight, TextBlock? label = null)
            {
                Root = root;
                HalfWidth = halfWidth;
                HalfHeight = halfHeight;
                Label = label;
            }

            public FrameworkElement Root { get; }
            public double HalfWidth { get; }
            public double HalfHeight { get; }
            public TextBlock? Label { get; }
            public bool? Glowing { get; set; }
        }

        public MainWindow()
        {
            InitializeComponent();
            Closed += (_, _) => StopBlend();
            InitializeSimulation();
        }

        private void InitializeSimulation()
        {
            try
            {
                var plansDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plans");
                var result = GreenhouseFactory.Create(plansDir);
                _controller = result.controller;
                _controller.AlertTriggered += OnAlertTriggered;

                PlansComboBox.ItemsSource = _controller.ListPlans();
                if (PlansComboBox.Items.Count > 0)
                {
                    PlansComboBox.SelectedIndex = 0;
                }

                _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _timer.Tick += Timer_Tick;

                InitHeatmap();
                UpdateUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации: {ex.Message}");
            }
        }

        private void InitHeatmap()
        {
            StopBlend();
            _heatmapBitmap = null;
            _heatmapMode = 0;
            _gridCellsX = 0;
            _gridCellsY = 0;
        }

        private void UpdateHeatmap()
        {
            if (_controller?.Greenhouse == null)
            {
                return;
            }

            var hall = _controller.Greenhouse;
            var grid = hall.Grid;
            int cellsX = grid.WidthCells;
            int cellsY = grid.LengthCells;
            double canvasWidth = GreenhouseCanvas.ActualWidth;
            double canvasHeight = GreenhouseCanvas.ActualHeight;
            if (cellsX <= 0 || cellsY <= 0 || canvasWidth < 1 || canvasHeight < 1 || hall.Width <= 0 || hall.Length <= 0)
            {
                return;
            }

            double step = hall.Config.GridStep;
            double fieldWidth = canvasWidth * cellsX * step / hall.Width;
            double fieldHeight = canvasHeight * cellsY * step / hall.Length;
            UpdateGridLines(fieldWidth, fieldHeight, cellsX, cellsY);

            int mode = HeatmapComboBox.SelectedIndex;
            if (mode <= 0)
            {
                StopBlend();
                _heatmapMode = 0;
                LegendPanel.Visibility = Visibility.Collapsed;
                if (_heatmapImage != null)
                {
                    _heatmapImage.Visibility = Visibility.Collapsed;
                }

                return;
            }

            bool resized = EnsureHeatmapSurface(cellsX, cellsY);
            _heatmapImage!.Visibility = Visibility.Visible;
            _heatmapImage.Width = fieldWidth;
            _heatmapImage.Height = fieldHeight;

            int rampCount = BuildRamp(mode, _rampStops, _rampColors);
            SampleLayer(mode, grid);

            if (resized || mode != _heatmapMode)
            {
                _heatmapMode = mode;
                _rampCount = rampCount;
                StopBlend();
                Array.Copy(_rampStops, _rampFromStops, rampCount);
                Array.Copy(_rampStops, _shownStops, rampCount);
                BuildRampTable(_shownStops, rampCount);
                Array.Copy(_blendTo, _shownValues, _blendTo.Length);
                UpdateLegend(mode, rampCount);
                PaintHeatmap();
                return;
            }

            // A new corridor must not recolour the hall before the temperatures have moved.
            Array.Copy(_shownStops, _rampFromStops, _rampCount);
            _rampCount = rampCount;
            Array.Copy(_shownValues, _blendFrom, _shownValues.Length);
            UpdateLegend(mode, rampCount);
            StartBlend();
        }

        private void SampleLayer(int mode, EnvironmentGrid grid)
        {
            int index = 0;
            for (int j = 0; j < _cellsY; j++)
            {
                for (int i = 0; i < _cellsX; i++)
                {
                    ClimateCell cell = grid.GetCell(i, j);
                    _blendTo[index++] = mode switch
                    {
                        1 => cell.Temperature,
                        2 => cell.RelativeHumidity,
                        3 => cell.Light,
                        _ => cell.Ph
                    };
                }
            }
        }

        private void StartBlend()
        {
            _blendClock.Restart();
            if (!_blending)
            {
                _blending = true;
                CompositionTarget.Rendering += OnHeatmapFrame;
            }
        }

        private void StopBlend()
        {
            if (_blending)
            {
                _blending = false;
                CompositionTarget.Rendering -= OnHeatmapFrame;
            }
        }

        private void OnHeatmapFrame(object? sender, EventArgs e)
        {
            double t = Math.Min(1.0, _blendClock.Elapsed.TotalMilliseconds / HeatmapBlendMilliseconds);
            double eased = t * t * (3.0 - 2.0 * t);
            bool stopsMoved = false;
            for (int s = 0; s < _rampCount; s++)
            {
                double stop = _rampFromStops[s] + (_rampStops[s] - _rampFromStops[s]) * eased;
                stopsMoved |= Math.Abs(stop - _shownStops[s]) > 1e-6;
                _shownStops[s] = stop;
            }

            if (stopsMoved)
            {
                BuildRampTable(_shownStops, _rampCount);
            }

            for (int k = 0; k < _shownValues.Length; k++)
            {
                _shownValues[k] = _blendFrom[k] + (_blendTo[k] - _blendFrom[k]) * eased;
            }

            PaintHeatmap();
            if (t >= 1.0)
            {
                StopBlend();
                UpdateLegend(_heatmapMode, _rampCount);
            }
        }

        // Cell values sit at cell centres, exactly like EnvironmentGrid.GetInterpolatedValue samples them for sensors.
        private void PaintHeatmap()
        {
            if (_heatmapBitmap == null)
            {
                return;
            }

            int width = _heatmapBitmap.PixelWidth;
            int height = _heatmapBitmap.PixelHeight;
            int pixel = 0;
            for (int py = 0; py < height; py++)
            {
                int low = _rowLow[py] * _cellsX;
                int high = _rowHigh[py] * _cellsX;
                double ty = _rowWeight[py];
                for (int px = 0; px < width; px++)
                {
                    int left = _columnLow[px];
                    int right = _columnHigh[px];
                    double tx = _columnWeight[px];
                    double top = _shownValues[low + left] + (_shownValues[low + right] - _shownValues[low + left]) * tx;
                    double bottom = _shownValues[high + left] + (_shownValues[high + right] - _shownValues[high + left]) * tx;
                    _heatmapPixels[pixel++] = RampColor(top + (bottom - top) * ty);
                }
            }

            _heatmapBitmap.WritePixels(new Int32Rect(0, 0, width, height), _heatmapPixels, width * 4, 0);
        }

        private int RampColor(double value)
        {
            int index = (int)((value - _rampMin) * _rampScale + 0.5);
            if (index < 0)
            {
                index = 0;
            }
            else if (index >= RampResolution)
            {
                index = RampResolution - 1;
            }

            return _rampTable[index];
        }

        private void BuildRampTable(double[] stops, int count)
        {
            _rampMin = stops[0];
            _rampScale = (RampResolution - 1) / Math.Max(1e-6, stops[count - 1] - _rampMin);
            for (int k = 0; k < RampResolution; k++)
            {
                Color color = MapRamp(_rampMin + k / _rampScale, stops, _rampColors, count);
                _rampTable[k] = (255 << 24) | (color.R << 16) | (color.G << 8) | color.B;
            }
        }

        private void UpdateLegend(int mode, int count)
        {
            LegendPanel.Visibility = Visibility.Visible;
            bool scaleChanged = mode != _legendMode || count != _legendCount;
            for (int k = 0; k < count && !scaleChanged; k++)
            {
                scaleChanged = Math.Abs(_legendStops[k] - _shownStops[k]) > 1e-9;
            }

            if (scaleChanged)
            {
                _legendMode = mode;
                _legendCount = count;
                Array.Copy(_shownStops, _legendStops, count);
                TxtLegendTitle.Text = LayerTitle(mode);

                double first = _shownStops[0];
                double span = Math.Max(1e-6, _shownStops[count - 1] - first);
                var gradient = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
                for (int k = 0; k < count; k++)
                {
                    gradient.GradientStops.Add(new GradientStop(_rampColors[k], (_shownStops[k] - first) / span));
                }

                gradient.Freeze();
                LegendBar.Fill = gradient;
                LayoutLegendTicks();
            }

            double low = double.PositiveInfinity;
            double high = double.NegativeInfinity;
            double sum = 0.0;
            foreach (double value in _blendTo)
            {
                low = Math.Min(low, value);
                high = Math.Max(high, value);
                sum += value;
            }

            _legendLow = low;
            _legendHigh = high;
            string unit = LayerUnit(mode);
            TxtLegendRange.Text = $"В зале: {FormatLayer(mode, low)}…{FormatLayer(mode, high)}{unit}, среднее {FormatLayer(mode, sum / Math.Max(1, _blendTo.Length))}{unit}";
            PlaceLegendRange();
        }

        private void LegendTicks_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            LayoutLegendTicks();
        }

        private void LayoutLegendTicks()
        {
            LegendTicks.Children.Clear();
            double width = LegendTicks.ActualWidth;
            if (_legendCount < 2 || width <= 0)
            {
                return;
            }

            double first = _legendStops[0];
            double span = Math.Max(1e-6, _legendStops[_legendCount - 1] - first);
            bool corridorScale = _legendMode is 1 or 2 or 4;
            int corridorFirst = _legendMode == 1 ? 2 : 1;
            for (int k = 0; k < _legendCount; k++)
            {
                double x = (_legendStops[k] - first) / span * width;
                bool corridor = corridorScale && k >= corridorFirst && k <= corridorFirst + 2;
                var tick = new Rectangle { Width = 1, Height = 4, Fill = Brushes.DimGray };
                Canvas.SetLeft(tick, x - 0.5);
                LegendTicks.Children.Add(tick);

                var label = new TextBlock
                {
                    Text = FormatLayer(_legendMode, _legendStops[k]),
                    FontSize = 10.5,
                    FontWeight = corridor ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = corridor ? Brushes.Black : Brushes.DimGray
                };
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(label, Math.Clamp(x - label.DesiredSize.Width / 2, 0, Math.Max(0, width - label.DesiredSize.Width)));
                Canvas.SetTop(label, 4);
                LegendTicks.Children.Add(label);
            }

            PlaceLegendRange();
        }

        private void PlaceLegendRange()
        {
            double width = LegendBar.ActualWidth;
            if (_legendCount < 2 || width <= 0 || double.IsInfinity(_legendLow))
            {
                LegendRange.Visibility = Visibility.Collapsed;
                return;
            }

            double first = _legendStops[0];
            double span = Math.Max(1e-6, _legendStops[_legendCount - 1] - first);
            double left = Math.Clamp((_legendLow - first) / span, 0.0, 1.0) * width;
            double right = Math.Clamp((_legendHigh - first) / span, 0.0, 1.0) * width;
            LegendRange.Visibility = Visibility.Visible;
            LegendRange.Width = Math.Max(3.0, right - left);
            Canvas.SetLeft(LegendRange, Math.Min(left, width - LegendRange.Width));
        }

        private static string LayerTitle(int mode) => mode switch
        {
            1 => "Температура",
            2 => "Влажность",
            3 => "Освещённость",
            _ => "pH раствора"
        };

        private static string LayerUnit(int mode) => mode switch
        {
            1 => " °C",
            2 => " %",
            _ => ""
        };

        private static string FormatLayer(int mode, double value) => mode switch
        {
            1 => value.ToString("0.0"),
            2 or 3 => value.ToString("0"),
            _ => value.ToString("0.00")
        };

        private bool EnsureHeatmapSurface(int cellsX, int cellsY)
        {
            int width = cellsX * PixelsPerCell;
            int height = cellsY * PixelsPerCell;
            if (_heatmapBitmap != null && _heatmapBitmap.PixelWidth == width && _heatmapBitmap.PixelHeight == height)
            {
                return false;
            }

            _cellsX = cellsX;
            _cellsY = cellsY;
            _heatmapBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            _heatmapPixels = new int[width * height];
            _shownValues = new double[cellsX * cellsY];
            _blendFrom = new double[cellsX * cellsY];
            _blendTo = new double[cellsX * cellsY];
            (_columnLow, _columnHigh, _columnWeight) = BuildAxis(width, cellsX);
            (_rowLow, _rowHigh, _rowWeight) = BuildAxis(height, cellsY);

            if (_heatmapImage == null)
            {
                _heatmapImage = new Image
                {
                    Stretch = Stretch.Fill,
                    IsHitTestVisible = false
                };
                RenderOptions.SetBitmapScalingMode(_heatmapImage, BitmapScalingMode.Linear);
                Canvas.SetLeft(_heatmapImage, 0);
                Canvas.SetTop(_heatmapImage, 0);
                HeatmapCanvas.Children.Add(_heatmapImage);
            }

            _heatmapImage.Source = _heatmapBitmap;
            return true;
        }

        private static (int[] Low, int[] High, double[] Weight) BuildAxis(int pixels, int cells)
        {
            var low = new int[pixels];
            var high = new int[pixels];
            var weight = new double[pixels];
            for (int p = 0; p < pixels; p++)
            {
                double position = Math.Clamp((p + 0.5) / PixelsPerCell - 0.5, 0.0, cells - 1);
                int cell = (int)position;
                low[p] = cell;
                high[p] = Math.Min(cell + 1, cells - 1);
                weight[p] = position - cell;
            }

            return (low, high, weight);
        }

        private void UpdateGridLines(double fieldWidth, double fieldHeight, int cellsX, int cellsY)
        {
            if (_gridLine != null
                && _gridCellsX == cellsX
                && _gridCellsY == cellsY
                && Math.Abs(_gridFieldWidth - fieldWidth) < 0.5
                && Math.Abs(_gridFieldHeight - fieldHeight) < 0.5)
            {
                return;
            }

            _gridCellsX = cellsX;
            _gridCellsY = cellsY;
            _gridFieldWidth = fieldWidth;
            _gridFieldHeight = fieldHeight;

            double cellWidth = fieldWidth / cellsX;
            double cellHeight = fieldHeight / cellsY;
            var geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                for (int i = 0; i <= cellsX; i++)
                {
                    double x = i * cellWidth;
                    context.BeginFigure(new Point(x, 0), false, false);
                    context.LineTo(new Point(x, fieldHeight), true, false);
                }

                for (int j = 0; j <= cellsY; j++)
                {
                    double y = j * cellHeight;
                    context.BeginFigure(new Point(0, y), false, false);
                    context.LineTo(new Point(fieldWidth, y), true, false);
                }
            }

            geometry.Freeze();
            if (_gridHalo == null || _gridLine == null)
            {
                _gridHalo = CreateGridPath(Color.FromArgb(55, 255, 255, 255), 1.4);
                _gridLine = CreateGridPath(Color.FromArgb(80, 20, 28, 36), 0.7);
                GridCanvas.Children.Add(_gridHalo);
                GridCanvas.Children.Add(_gridLine);
            }

            _gridHalo.Data = geometry;
            _gridLine.Data = geometry;
        }

        private static Path CreateGridPath(Color color, double thickness)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return new Path
            {
                Stroke = brush,
                StrokeThickness = thickness,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true
            };
        }

        private int BuildRamp(int mode, double[] stops, Color[] colors)
        {
            if (mode == 1)
            {
                return BuildTemperatureRamp(stops, colors);
            }

            if (mode == 2)
            {
                ParameterCorridor humidity = ActivePhase()?.HumidityCorridor ?? new ParameterCorridor(60, 80, 70);
                SetCorridorStops(stops, humidity, 25, 12, 0.5);
                colors[0] = Color.FromRgb(224, 190, 86);
                colors[1] = Color.FromRgb(214, 214, 150);
                colors[2] = Color.FromRgb(120, 200, 200);
                colors[3] = Color.FromRgb(40, 150, 220);
                colors[4] = Color.FromRgb(20, 50, 170);
                return 5;
            }

            if (mode == 3)
            {
                double saturation = Math.Max(20.0, _controller.Greenhouse.Config.LightSaturation);
                stops[0] = 0;
                stops[1] = saturation * 0.2;
                stops[2] = saturation;
                stops[3] = saturation * 1.6;
                colors[0] = Color.FromRgb(30, 36, 54);
                colors[1] = Color.FromRgb(78, 88, 112);
                colors[2] = Color.FromRgb(255, 210, 70);
                colors[3] = Color.FromRgb(255, 252, 240);
                return 4;
            }

            ParameterCorridor ph = ActivePhase()?.PhCorridor ?? new ParameterCorridor(5.5, 6.5, 6.0);
            SetCorridorStops(stops, ph, 1.0, 1.0, 0.01);
            colors[0] = Color.FromRgb(206, 48, 28);
            colors[1] = Color.FromRgb(238, 142, 40);
            colors[2] = Color.FromRgb(64, 172, 82);
            colors[3] = Color.FromRgb(142, 72, 190);
            colors[4] = Color.FromRgb(42, 70, 200);
            return 5;
        }

        private static void SetCorridorStops(double[] stops, ParameterCorridor corridor, double below, double above, double gap)
        {
            stops[0] = corridor.MinValue - below;
            stops[1] = corridor.MinValue;
            stops[2] = corridor.TargetValue;
            stops[3] = corridor.MaxValue;
            stops[4] = corridor.MaxValue + above;
            for (int i = 1; i < 5; i++)
            {
                stops[i] = Math.Max(stops[i], stops[i - 1] + gap);
            }
        }

        // Green spans the plan corridor; blue and red open up a few degrees beyond it, so a device plume shows without repainting the hall.
        private int BuildTemperatureRamp(double[] stops, Color[] colors)
        {
            double min = 20;
            double target = 22;
            double max = 24;
            ParameterCorridor? corridor = ActiveTemperatureCorridor();
            if (corridor != null)
            {
                min = corridor.MinValue;
                target = corridor.TargetValue;
                max = corridor.MaxValue;
            }

            stops[0] = min - 6;
            stops[1] = min - 2;
            stops[2] = min;
            stops[3] = target;
            stops[4] = max;
            stops[5] = max + 2;
            stops[6] = max + 6;
            for (int i = 1; i < 7; i++)
            {
                stops[i] = Math.Max(stops[i], stops[i - 1] + 0.05);
            }

            colors[0] = Color.FromRgb(28, 60, 200);
            colors[1] = Color.FromRgb(56, 150, 230);
            colors[2] = Color.FromRgb(80, 196, 196);
            colors[3] = Color.FromRgb(64, 176, 78);
            colors[4] = Color.FromRgb(214, 200, 48);
            colors[5] = Color.FromRgb(242, 128, 36);
            colors[6] = Color.FromRgb(204, 36, 30);
            return 7;
        }

        private ParameterCorridor? ActiveTemperatureCorridor()
        {
            (int _, int hour) = AirClock();
            return ActivePhase()?.GetTempCorridor(hour);
        }

        private PlanPhase? ActivePhase()
        {
            GrowingPlan? plan = _controller.ActivePlan;
            (int day, int _) = AirClock();
            if (plan == null || day > plan.TotalDurationDays)
            {
                return null;
            }

            return plan.GetPhaseForDay(day);
        }

        private (int day, int hour) AirClock()
        {
            int elapsed = _controller.ElapsedHours;
            if (elapsed > 0)
            {
                elapsed--;
            }

            return (elapsed / 24 + 1, elapsed % 24);
        }

        private static Color MapRamp(double value, double[] stops, Color[] colors, int count)
        {
            if (count <= 0)
            {
                return Colors.Transparent;
            }

            if (value <= stops[0])
            {
                return colors[0];
            }

            for (int i = 1; i < count; i++)
            {
                if (value <= stops[i])
                {
                    double span = stops[i] - stops[i - 1];
                    double t = span <= 1e-6 ? 0.0 : (value - stops[i - 1]) / span;
                    return Mix(colors[i - 1], colors[i], t);
                }
            }

            return colors[count - 1];
        }

        private static Color Mix(Color from, Color to, double t)
        {
            t = Math.Clamp(t, 0.0, 1.0);
            return Color.FromRgb(
                (byte)(from.R + (to.R - from.R) * t),
                (byte)(from.G + (to.G - from.G) * t),
                (byte)(from.B + (to.B - from.B) * t));
        }

        private void HeatmapComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_controller != null) UpdateHeatmap();
        }

        // --- УПРАВЛЕНИЕ СИМУЛЯЦИЕЙ ---
        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (PlansComboBox.SelectedItem == null) return;
            try
            {
                _controller.StartCycle(PlansComboBox.SelectedItem.ToString());
                _timer.Start();

                BtnStart.IsEnabled = false; PlansComboBox.IsEnabled = false;
                if (BtnOpenEditor != null) BtnOpenEditor.IsEnabled = false;
                BtnPause.IsEnabled = true; BtnStop.IsEnabled = true;
                BtnPause.Content = "⏸ Пауза";

                TxtStatus.Text = "Статус: Выращиваем...";
                
                _currentAlerts.Clear();
                TxtHistory.Text = "--- Цикл запущен ---\n";
                _actuatorStates.Clear();
                _climateRegime = null;
                CheckActuatorChanges();

                UpdateUI();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            if (_controller.IsPaused)
            {
                _controller.Resume();
                BtnPause.Content = "⏸ Пауза";
                TxtStatus.Text = "Статус: Выращиваем...";
            }
            else
            {
                _controller.Pause();
                BtnPause.Content = "▶ Продолжить";
                TxtStatus.Text = "Статус: На паузе";
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            _controller.StopCycle();
            FinishStoppedCycle();
        }

        private void BtnOpenEditor_Click(object sender, RoutedEventArgs e)
        {
            var editor = new LayoutEditorWindow(_controller.Greenhouse);
            editor.Owner = this;
            if (editor.ShowDialog() == true)
            {
                _controller.BindNewSensors();
                _controller.SyncRegulation();
                if (_controller.IsRunning)
                {
                    CheckActuatorChanges(schemeEdited: true);
                }
                InitHeatmap(); // Сетка могла измениться
                UpdateUI();
            }
        }

        // --- ДОБАВЛЕНИЕ/РЕДАКТИРОВАНИЕ ПЛАНА ---
        private void BtnShowNewPlan_Click(object sender, RoutedEventArgs e)
        {
            _editingExistingPlan = false;
            PlanFormTitle.Text = "Новый план";
            InputCropName.IsEnabled = true;
            InputCropName.Text = "";
            InputDuration.Text = "1";
            SetCorridorInputs(InputDayTempMin, InputDayTempTarget, InputDayTempMax, 23, 25, 27);
            SetCorridorInputs(InputNightTempMin, InputNightTempTarget, InputNightTempMax, 20, 22, 24);
            InputDayLength.Text = "16";
            SetCorridorInputs(InputHumMin, InputHumTarget, InputHumMax, 60, 65, 70);
            SetCorridorInputs(InputPhMin, InputPhTarget, InputPhMax, 5.5, 6, 6.5);
            InputLight.Text = "14";
            OverlayNewPlan.Visibility = Visibility.Visible;
        }

        private void BtnEditPlan_Click(object sender, RoutedEventArgs e)
        {
            string? selected = PlansComboBox.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(selected))
            {
                MessageBox.Show("Сначала выберите план.", "План", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                GrowingPlan plan = _controller.LoadPlan(selected);
                PlanPhase phase = plan.GetPhaseForDay(1);

                _editingExistingPlan = true;
                PlanFormTitle.Text = "Изменить план";
                InputCropName.IsEnabled = false;
                InputCropName.Text = plan.CropName;
                InputDuration.Text = plan.TotalDurationDays.ToString();
                SetCorridorInputs(InputDayTempMin, InputDayTempTarget, InputDayTempMax, phase.TempCorridor);
                SetCorridorInputs(InputNightTempMin, InputNightTempTarget, InputNightTempMax, phase.NightTempCorridor ?? phase.TempCorridor);
                InputDayLength.Text = phase.DayDurationHours.ToString();
                SetCorridorInputs(InputHumMin, InputHumTarget, InputHumMax, phase.HumidityCorridor);
                SetCorridorInputs(InputPhMin, InputPhTarget, InputPhMax, phase.PhCorridor);
                InputLight.Text = phase.LightDurationHours.ToString();
                OverlayNewPlan.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть план:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancelPlan_Click(object sender, RoutedEventArgs e)
        {
            OverlayNewPlan.Visibility = Visibility.Collapsed;
        }

        private void BtnSavePlan_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int days = int.Parse(InputDuration.Text);
                int dayLength = int.Parse(InputDayLength.Text);
                int lightHours = int.Parse(InputLight.Text);
                ParameterCorridor dayTemp = ReadCorridor(InputDayTempMin, InputDayTempTarget, InputDayTempMax);
                ParameterCorridor nightTemp = ReadCorridor(InputNightTempMin, InputNightTempTarget, InputNightTempMax);
                ParameterCorridor humidity = ReadCorridor(InputHumMin, InputHumTarget, InputHumMax);
                ParameterCorridor ph = ReadCorridor(InputPhMin, InputPhTarget, InputPhMax);

                var plan = new GrowingPlan(InputCropName.Text);
                for (int day = 1; day <= days; day++)
                {
                    plan.AddPhase(new PlanPhase(
                        day, dayTemp, ph, humidity, lightHours, nightTemp, dayLength));
                }

                if (_editingExistingPlan) _controller.UpdatePlan(plan);
                else _controller.AddPlan(plan);

                PlansComboBox.ItemsSource = _controller.ListPlans();
                PlansComboBox.SelectedItem = plan.CropName;
                OverlayNewPlan.Visibility = Visibility.Collapsed;

                if (_controller.IsRunning)
                {
                    _controller.SyncRegulation();
                    CheckActuatorChanges();
                }

                UpdateUI();
                string action = _editingExistingPlan ? "обновлён" : "создан";
                MessageBox.Show($"План '{plan.CropName}' {action} и сохранён.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (ArgumentOutOfRangeException ex) when (ex.ParamName is "minValue" or "maxValue" or "targetValue")
            {
                MessageBox.Show("Минимум должен быть меньше максимума, а идеальное значение — между ними.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении плана:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static double ParseUserNumber(string text) => double.Parse(text.Trim().Replace('.', ','));
        private static void SetCorridorInputs(TextBox min, TextBox target, TextBox max, ParameterCorridor corridor) => SetCorridorInputs(min, target, max, corridor.MinValue, corridor.TargetValue, corridor.MaxValue);
        private static void SetCorridorInputs(TextBox min, TextBox target, TextBox max, double minValue, double targetValue, double maxValue)
        {
            min.Text = FormatPlanNumber(minValue);
            target.Text = FormatPlanNumber(targetValue);
            max.Text = FormatPlanNumber(maxValue);
        }
        private static ParameterCorridor ReadCorridor(TextBox min, TextBox target, TextBox max) => new ParameterCorridor(ParseUserNumber(min.Text), ParseUserNumber(max.Text), ParseUserNumber(target.Text));
        private static string FormatPlanNumber(double value) => value.ToString("0.##");

        // --- ЛОГИКА ТАЙМЕРА И ОТЧЕТА ---
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_controller.IsPaused) return;

            if (!_controller.IsRunning)
            {
                FinishStoppedCycle();
                return;
            }

            _currentAlerts.Clear();
            try { _controller.SimulationTick(); }
            catch { FinishStoppedCycle(); return; }

            if (!_controller.IsRunning)
            {
                CheckActuatorChanges(clockAdvanced: true);
                FinishStoppedCycle();
                return;
            }

            CheckActuatorChanges(clockAdvanced: true);
            UpdateUI();
        }

        private void FinishStoppedCycle()
        {
            _timer.Stop();

            BtnStart.IsEnabled = true; PlansComboBox.IsEnabled = true;
            if (BtnOpenEditor != null) BtnOpenEditor.IsEnabled = true;
            BtnPause.IsEnabled = false; BtnStop.IsEnabled = false;

            TxtStatus.Text = "Статус: Остановлено";
            TxtHeader.Text = "Ожидание запуска...";
            UpdateUI();
            
            // Вызов Окна Отчета (REP-02)
            SimulationResult result = _controller.TakeResult(out string? failureMessage);
            if (result != SimulationResult.None)
            {
                var reportWindow = new CycleReportWindow(_controller, result, failureMessage);
                reportWindow.Owner = this;
                reportWindow.ShowDialog();
            }
        }

        // --- ЛОГИРОВАНИЕ ---
        private void CheckActuatorChanges(bool schemeEdited = false, bool clockAdvanced = false)
        {
            int elapsed = _controller.ElapsedHours;
            if (clockAdvanced && elapsed > 0) elapsed--;

            int day = elapsed / 24 + 1;
            int hour = elapsed % 24;
            string timeStamp = $"[День {day}, {hour}:00]";
            LogClimateRegime(timeStamp, day, hour);
            var gh = _controller.Greenhouse;
            List<Actuator> actuators = gh.GetAllActuators();
            
            string temperature = $"{gh.CurrentTemperature:F1}°C";
            string humidity = $"{gh.CurrentHumidity:F1}%";
            string ph = $"pH {gh.CurrentPh:F2}";

            LogRegulatorGroup(timeStamp, actuators, a => a is Heater,
                count => $"Для повышения температуры – включены обогреватели ({count} шт.). (Факт: {temperature})",
                $"Температура стабилизировалась ({temperature}) - обогреватель выключен.",
                prev => $"Со схемы убраны обогреватели ({prev} шт.) — нагрев прекращён. (Факт: {temperature})",
                (prev, cur) => $"Работающих обогревателей: {cur} шт. (было {prev}). (Факт: {temperature})",
                (prev, cur) => cur == 0 ? $"Со схемы убраны обогреватели ({prev} шт.)." : $"Обогревателей на схеме: {cur} шт. (было {prev}), сейчас выключены.",
                schemeEdited);

            LogRegulatorGroup(timeStamp, actuators, a => a is AirConditioner,
                count => $"Для понижения температуры – включены кондиционеры ({count} шт.). (Факт: {temperature})",
                $"Температура стабилизировалась ({temperature}) - кондиционер выключен.",
                prev => $"Со схемы убраны кондиционеры ({prev} шт.) — охлаждение прекращено. (Факт: {temperature})",
                (prev, cur) => $"Работающих кондиционеров: {cur} шт. (было {prev}). (Факт: {temperature})",
                (prev, cur) => cur == 0 ? $"Со схемы убраны кондиционеры ({prev} шт.)." : $"Кондиционеров на схеме: {cur} шт. (было {prev}), сейчас выключены.",
                schemeEdited);

            LogRegulatorGroup(timeStamp, actuators, a => a is Humidifier,
                count => $"Для повышения влажности – включены увлажнители ({count} шт.). (Факт: {humidity})",
                $"Влажность стабилизировалась ({humidity}) - увлажнитель выключен.",
                prev => $"Со схемы убраны увлажнители ({prev} шт.) — увлажнение прекращено. (Факт: {humidity})",
                (prev, cur) => $"Работающих увлажнителей: {cur} шт. (было {prev}). (Факт: {humidity})",
                (prev, cur) => cur == 0 ? $"Со схемы убраны увлажнители ({prev} шт.)." : $"Увлажнителей на схеме: {cur} шт. (было {prev}), сейчас выключены.",
                schemeEdited);

            LogRegulatorGroup(timeStamp, actuators, a => a is FertilizerDoser,
                count => $"Для регулировки pH – активированы дозаторы ({count} шт.). (Факт: {ph})",
                $"pH нормализовался ({gh.CurrentPh:F2}) - дозатор выключен.",
                prev => $"Со схемы убраны дозаторы ({prev} шт.) — корректировка pH прекращена. (Факт: {ph})",
                (prev, cur) => $"Работающих дозаторов: {cur} шт. (было {prev}). (Факт: {ph})",
                (prev, cur) => cur == 0 ? $"Со схемы убраны дозаторы ({prev} шт.)." : $"Дозаторов на схеме: {cur} шт. (было {prev}), сейчас выключены.",
                schemeEdited);

            _actuatorStates.Clear();
            foreach (Actuator actuator in actuators)
            {
                _actuatorStates[actuator] = actuator.IsOn;
            }
        }

        private void LogRegulatorGroup(string timeStamp, IReadOnlyList<Actuator> actuators, Func<Actuator, bool> match, Func<int, string> turnedOn, string turnedOff, Func<int, string> removedWhileWorking, Func<int, int, string> resized, Func<int, int, string> idleCountChanged, bool schemeEdited)
{
    int previousTotal = 0, previousOn = 0, currentTotal = 0, currentOn = 0;
    foreach (var pair in _actuatorStates) { if (match(pair.Key)) { previousTotal++; if (pair.Value) previousOn++; } }
    foreach (var a in actuators) { if (match(a)) { currentTotal++; if (a.IsOn) currentOn++; } }

    if (previousTotal == currentTotal && previousOn == currentOn) return;

    string? line = null;
    if (currentOn > 0 && previousOn == 0) line = turnedOn(currentOn);
    else if (currentOn == 0 && previousOn > 0 && currentTotal == 0) line = removedWhileWorking(previousTotal);
    else if (currentOn == 0 && previousOn > 0) line = turnedOff;
    else if (currentOn > 0 && previousOn > 0 && currentOn != previousOn) line = resized(previousOn, currentOn);
    else if (previousTotal != currentTotal && (previousTotal > 0 || schemeEdited)) line = idleCountChanged(previousTotal, currentTotal);

    if (!string.IsNullOrEmpty(line))
    {
        TxtHistory.Text += $"{timeStamp} {line}\n\n";
        HistoryScroll.ScrollToEnd();
    }
}

private void LogClimateRegime(string timeStamp, int day, int hour)
{
    if (!TryDescribeClimate(day, hour, out string description) || _climateRegime == description) return;
    _climateRegime = description;
    TxtHistory.Text += $"{timeStamp} {description}\n\n";
    HistoryScroll.ScrollToEnd();
}

        private bool TryDescribeClimate(out string description)
        {
            (int day, int hour) = AirClock();
            return TryDescribeClimate(day, hour, out description);
        }

        private bool TryDescribeClimate(int day, int hour, out string description)
        {
            description = "";
            if (_controller.ActivePlan == null || day > _controller.ActivePlan.TotalDurationDays) return false;

            PlanPhase phase = _controller.ActivePlan.GetPhaseForDay(day);
            ParameterCorridor corridor = phase.GetTempCorridor(hour);
            string period = phase.IsDaytime(hour) ? "День" : "Ночь";
            description = $"{period}. Целевая температура {corridor.TargetValue:0.##}°C (коридор {corridor.MinValue:0.##}–{corridor.MaxValue:0.##}).";
            return true;
        }

        private void OnAlertTriggered(string sensorId, double badValue)
        {
            _currentAlerts[sensorId] = $"• {sensorId}: {badValue:F1}";
        }

        // --- ГРАФИКА И ОБНОВЛЕНИЕ ЭКРАНА ---
        private void GreenhouseCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateUI();
        }

        private void PlanHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            FitPlanToHall();
        }

        private const string CellProbeHint = "Наведите курсор на клетку, чтобы увидеть её показатели";

        private void GreenhouseCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            (int X, int Y)? cell = CellUnder(e.GetPosition(GreenhouseCanvas));
            if (cell == _probedCell)
            {
                return;
            }

            _probedCell = cell;
            UpdateCellProbe();
        }

        private void GreenhouseCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            _probedCell = null;
            UpdateCellProbe();
        }

        private (int X, int Y)? CellUnder(Point point)
        {
            if (_gridCellsX <= 0 || _gridCellsY <= 0 || _gridFieldWidth <= 0 || _gridFieldHeight <= 0 || point.X < 0 || point.Y < 0)
            {
                return null;
            }

            int i = (int)(point.X / (_gridFieldWidth / _gridCellsX));
            int j = (int)(point.Y / (_gridFieldHeight / _gridCellsY));
            return i < _gridCellsX && j < _gridCellsY ? (i, j) : null;
        }

        private void UpdateCellProbe()
        {
            EnvironmentGrid? grid = _controller?.Greenhouse?.Grid;
            if (grid == null || _probedCell is not { } probe || probe.X >= grid.WidthCells || probe.Y >= grid.LengthCells)
            {
                TxtCellProbe.Text = CellProbeHint;
                return;
            }

            ClimateCell cell = grid.GetCell(probe.X, probe.Y);
            double step = _controller!.Greenhouse.Config.GridStep;
            TxtCellProbe.Text =
                $"Клетка x {probe.X * step:0.0}–{(probe.X + 1) * step:0.0} м, y {probe.Y * step:0.0}–{(probe.Y + 1) * step:0.0} м:  " +
                $"{cell.Temperature:0.0} °C · {cell.RelativeHumidity:0} % · свет {cell.Light:0} · pH {cell.Ph:0.00} · EC {cell.Ec:0.00}";
        }

        private void FitPlanToHall()
        {
            var hall = _controller?.Greenhouse;
            if (hall == null || hall.Width <= 0 || hall.Length <= 0) return;

            Thickness frame = PlanFrame.BorderThickness;
            double frameWidth = frame.Left + frame.Right;
            double frameHeight = frame.Top + frame.Bottom;
            double width = PlanHost.ActualWidth - frameWidth;
            double height = PlanHost.ActualHeight - frameHeight;
            if (width <= 0 || height <= 0) return;

            double scale = Math.Min(width / hall.Width, height / hall.Length);
            PlanFrame.Width = Math.Floor(hall.Width * scale) + frameWidth;
            PlanFrame.Height = Math.Floor(hall.Length * scale) + frameHeight;
        }

        private void UpdateUI()
        {
            if (_controller == null || _controller.Greenhouse == null) return;

            FitPlanToHall();
            
            try
            {
                UpdateHeatmap();
            }
            catch (Exception ex)
            {
                if (_heatmapImage != null)
                {
                    _heatmapImage.Visibility = Visibility.Collapsed;
                }

                TxtStatus.Text = "Статус: ошибка теплокарты — " + ex.Message;
            }

            UpdateCellProbe();

            if (_controller.IsRunning)
            {
                if (_controller.StartDate.HasValue)
                {
                    int days = _controller.CurrentDay - 1;
                    int hours = _controller.HourOfDay;
                    string crop = _controller.ActivePlan?.CropName ?? "Неизвестно";
                    TxtHeader.Text = $"{crop}, начало цикла – {_controller.StartDate.Value:dd.MM.yyyy}, прошло: {days} суток, {hours} часов";
                }

                TxtRegime.Text = TryDescribeClimate(out string climate) ? climate : "";

                if (_currentAlerts.Count > 0)
                {
                    TxtAlerts.Text = "ВНИМАНИЕ! ОТКЛОНЕНИЯ:\n" + string.Join("\n", _currentAlerts.Values);
                    TxtAlerts.Foreground = Brushes.Red;
                }
                else
                {
                    TxtAlerts.Text = "Все показатели в пределах нормы";
                    TxtAlerts.Foreground = Brushes.DarkGreen;
                }
            }
            else
            {
                TxtAlerts.Text = "Симуляция остановлена";
                TxtAlerts.Foreground = Brushes.Gray;
                TxtRegime.Text = "";
            }

            DrawGreenhouseMap();
        }

        private void DrawGreenhouseMap()
        {
            double cw = GreenhouseCanvas.ActualWidth;
            double ch = GreenhouseCanvas.ActualHeight;
            if (cw == 0 || ch == 0) return;

            var gh = _controller.Greenhouse;
            double scaleX = cw / gh.Width;
            double scaleY = ch / gh.Length;
            var present = new HashSet<object>();

            foreach (Actuator actuator in gh.GetAllActuators())
            {
                present.Add(actuator);
                if (!_deviceVisuals.TryGetValue(actuator, out DeviceVisual? visual))
                {
                    visual = CreateActuatorVisual(actuator);
                    if (visual == null) continue;
                    AddDeviceVisual(actuator, visual, 0);
                }

                bool on = actuator.IsOn;
                if (visual.Glowing != on)
                {
                    visual.Root.Effect = on ? DeviceGlow : DeviceShadow;
                    visual.Glowing = on;
                }

                PlaceDevice(visual, actuator.Location, scaleX, scaleY);
            }

            foreach (Sensor sensor in gh.GetAllSensors())
            {
                present.Add(sensor);
                if (!_deviceVisuals.TryGetValue(sensor, out DeviceVisual? visual))
                {
                    visual = CreateSensorVisual(sensor);
                    AddDeviceVisual(sensor, visual, 1);
                }

                visual.Label!.Text = FormatSensorValue(sensor);
                PlaceDevice(visual, sensor.Location, scaleX, scaleY);
            }

            var removed = new List<object>();
            foreach (KeyValuePair<object, DeviceVisual> pair in _deviceVisuals)
            {
                if (!present.Contains(pair.Key))
                {
                    GreenhouseCanvas.Children.Remove(pair.Value.Root);
                    removed.Add(pair.Key);
                }
            }

            foreach (object device in removed)
            {
                _deviceVisuals.Remove(device);
            }
        }

        private void AddDeviceVisual(object device, DeviceVisual visual, int layer)
        {
            Panel.SetZIndex(visual.Root, layer);
            _deviceVisuals[device] = visual;
            GreenhouseCanvas.Children.Add(visual.Root);
        }

        private static void PlaceDevice(DeviceVisual visual, (double X, double Y) location, double scaleX, double scaleY)
        {
            Canvas.SetLeft(visual.Root, location.X * scaleX - visual.HalfWidth);
            Canvas.SetTop(visual.Root, location.Y * scaleY - visual.HalfHeight);
        }

        private static string FormatSensorValue(Sensor sensor)
        {
            return sensor.Type switch
            {
                SensorType.Temperature => Math.Round(sensor.CurrentValue, 1) + "°",
                SensorType.Humidity => Math.Round(sensor.CurrentValue) + "%",
                _ => Math.Round(sensor.CurrentValue, 1).ToString()
            };
        }

        // --- МЕТОДЫ РИСОВАНИЯ ФИГУР ---
        private static DropShadowEffect Frozen(DropShadowEffect effect)
        {
            effect.Freeze();
            return effect;
        }

        private LinearGradientBrush GetGradient(Color top, Color bottom)
        {
            var brush = new LinearGradientBrush(top, bottom, new Point(0, 0), new Point(0, 1));
            brush.Freeze();
            return brush;
        }

        private DeviceVisual CreateSensorVisual(Sensor sensor)
        {
            Shape sensorShape;
            Color textColor = Colors.White;

            if (sensor.Type == SensorType.Temperature)
            {
                sensorShape = new Ellipse { Width = 60, Height = 35, Fill = GetGradient(Colors.LightCyan, Colors.SkyBlue) };
                textColor = Colors.Black;
            }
            else if (sensor.Type == SensorType.Humidity)
            {
                sensorShape = new Ellipse { Width = 60, Height = 35, Fill = GetGradient(Color.FromRgb(70, 150, 160), Color.FromRgb(30, 100, 110)) };
            }
            else if (sensor.Type == SensorType.Ec)
            {
                sensorShape = new Ellipse { Width = 60, Height = 35, Fill = GetGradient(Color.FromRgb(90, 100, 170), Color.FromRgb(40, 50, 110)) };
            }
            else 
            {
                sensorShape = new Ellipse { Width = 60, Height = 35, Fill = GetGradient(Color.FromRgb(200, 100, 100), Color.FromRgb(150, 50, 50)) };
            }

            sensorShape.Effect = DeviceShadow;
            TextBlock tb = new TextBlock { Foreground = new SolidColorBrush(textColor), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 13, FontWeight = FontWeights.Bold };
            Grid g = new Grid { Width = 60, Height = 35 };
            g.Children.Add(sensorShape); g.Children.Add(tb);
            return new DeviceVisual(g, 30, 17, tb);
        }

        private DeviceVisual? CreateActuatorVisual(Actuator actuator)
        {
            if (actuator is Heater)
            {
                Border b = new Border { Width = 120, Height = 35, CornerRadius = new CornerRadius(5), Background = GetGradient(Color.FromRgb(170, 210, 120), Color.FromRgb(120, 160, 70)) };
                b.Child = new TextBlock { Text = "обогреватель", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Black };
                return new DeviceVisual(b, 60, 17);
            }

            if (actuator is AirConditioner)
            {
                Border outer = new Border { Width = 120, Height = 35, Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(100, 180, 200)), BorderThickness = new Thickness(4), CornerRadius = new CornerRadius(3) };
                Border inner = new Border { Margin = new Thickness(2), BorderBrush = new SolidColorBrush(Color.FromRgb(100, 180, 200)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2) };
                TextBlock tb = new TextBlock { Text = "кондиционер", FontSize = 12, Foreground = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                inner.Child = tb;
                outer.Child = inner;
                return new DeviceVisual(outer, 60, 17);
            }

            if (actuator is Humidifier)
            {
                Grid g = new Grid { Width = 50, Height = 50 };
                Polygon pOuter = new Polygon { Points = new PointCollection(new[] { new Point(15, 0), new Point(35, 0), new Point(50, 45), new Point(0, 45) }), Fill = new SolidColorBrush(Color.FromRgb(130, 90, 160)) };
                Polygon pInner = new Polygon { Points = new PointCollection(new[] { new Point(18, 5), new Point(32, 5), new Point(44, 40), new Point(6, 40) }), Stroke = Brushes.White, StrokeThickness = 2 };
                g.Children.Add(pOuter); g.Children.Add(pInner);
                return new DeviceVisual(g, 25, 25);
            }

            if (actuator is LightingSystem)
            {
                Grid g = new Grid { Width = 40, Height = 40 };
                Path rays = new Path { Data = Geometry.Parse("M 20,6 L 25,16 L 36,16 L 28,23 L 32,34 L 20,28 L 8,34 L 12,23 L 3,16 L 15,16 Z"), Fill = Brushes.Gold, Stroke = Brushes.Black, StrokeThickness = 1 };
                Ellipse core = new Ellipse { Width = 18, Height = 18, Fill = Brushes.Yellow, Stroke = Brushes.Black, StrokeThickness = 1, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                g.Children.Add(rays); g.Children.Add(core);
                return new DeviceVisual(g, 20, 20);
            }

            if (actuator is FertilizerDoser)
            {
                Path lightning = new Path { Data = Geometry.Parse("M 15,0 L 0,15 L 10,15 L 0,35 L 25,10 L 15,10 Z"), Fill = GetGradient(Color.FromRgb(80, 40, 20), Colors.Black) };
                return new DeviceVisual(lightning, 15, 20);
            }

            return null;
        }
    }
}