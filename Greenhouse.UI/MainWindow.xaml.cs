using GreenhouseSimulator.Core.Controllers;
using GreenhouseSimulator.Core.Models.Actuators;
using GreenhouseSimulator.Core.Models.Environment;
using GreenhouseSimulator.Core.Models.Plans;
using GreenhouseSimulator.Core.Models.Sensors;
using GreenhouseSimulator.Core.Services;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
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
        private List<string> _currentAlerts = new List<string>();
        private Dictionary<Actuator, bool> _actuatorStates = new Dictionary<Actuator, bool>();
        private string? _climateRegime;
        private bool _editingExistingPlan;

        private WriteableBitmap? _heatmapBitmap;
        private byte[]? _heatmapPixels;
        private Image? _heatmapImage;
        private Path? _gridShade;
        private Path? _gridLine;
        private int _gridCellsX;
        private int _gridCellsY;
        private double _gridCanvasWidth;
        private double _gridCanvasHeight;
        private readonly double[] _rampStops = new double[5];
        private readonly Color[] _rampColors = new Color[5];
        private double[] _sampleValues = Array.Empty<double>();

        public MainWindow()
        {
            InitializeComponent();
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
            _heatmapBitmap = null;
            _heatmapPixels = null;
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
            UpdateGridLines(canvasWidth, canvasHeight, cellsX, cellsY, step, hall.Width, hall.Length);

            int mode = HeatmapComboBox.SelectedIndex;
            if (mode <= 0)
            {
                if (_heatmapImage != null)
                {
                    _heatmapImage.Visibility = Visibility.Collapsed;
                }

                return;
            }

            if (!EnsureHeatmapSurface(cellsX, cellsY) || _heatmapBitmap == null || _heatmapPixels == null || _heatmapImage == null)
            {
                return;
            }

            _heatmapImage.Visibility = Visibility.Visible;
            _heatmapImage.Width = canvasWidth * (cellsX * step) / hall.Width;
            _heatmapImage.Height = canvasHeight * (cellsY * step) / hall.Length;
            Canvas.SetLeft(_heatmapImage, 0);
            Canvas.SetTop(_heatmapImage, 0);

            int rampCount = BuildRamp(mode, _rampStops, _rampColors);
            FillDisplaySamples(mode, grid, cellsX, cellsY, hall.GetAllActuators());

            int stride = cellsX * 4;
            int sampleIndex = 0;
            for (int y = 0; y < cellsY; y++)
            {
                for (int x = 0; x < cellsX; x++)
                {
                    Color color = MapRamp(_sampleValues[sampleIndex], _rampStops, _rampColors, rampCount);
                    sampleIndex++;
                    int pixel = (y * cellsX + x) * 4;
                    _heatmapPixels[pixel] = color.B;
                    _heatmapPixels[pixel + 1] = color.G;
                    _heatmapPixels[pixel + 2] = color.R;
                    _heatmapPixels[pixel + 3] = 255;
                }
            }

            _heatmapBitmap.WritePixels(new Int32Rect(0, 0, cellsX, cellsY), _heatmapPixels, stride, 0);
        }

        private void FillDisplaySamples(int mode, EnvironmentGrid grid, int cellsX, int cellsY, IReadOnlyList<Actuator> actuators)
        {
            int count = cellsX * cellsY;
            if (_sampleValues.Length != count)
            {
                _sampleValues = new double[count];
            }

            double biasSum = 0;
            int index = 0;
            for (int y = 0; y < cellsY; y++)
            {
                for (int x = 0; x < cellsX; x++)
                {
                    var cell = grid.GetCell(x, y);
                    double raw = mode switch
                    {
                        1 => cell.Temperature,
                        2 => cell.RelativeHumidity,
                        3 => cell.Light,
                        _ => cell.Ph
                    };
                    double bias = SpatialBias(mode, grid.GetCellCenter(x, y), actuators);
                    _sampleValues[index] = raw + bias;
                    biasSum += bias;
                    index++;
                }
            }

            double biasMean = biasSum / count;
            if (Math.Abs(biasMean) < 1e-9)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                _sampleValues[i] -= biasMean;
            }
        }

        private static double SpatialBias(int mode, (double X, double Y) point, IReadOnlyList<Actuator> actuators)
        {
            double bias = 0;
            foreach (Actuator actuator in actuators)
            {
                double falloff = actuator.GetAttenuation(point, true);
                if (falloff < 0.02)
                {
                    continue;
                }

                if (mode == 1)
                {
                    double power = VisualPower(actuator);
                    if (actuator is Heater)
                    {
                        bias += 12.0 * power * falloff;
                    }
                    else if (actuator is AirConditioner)
                    {
                        bias -= 12.0 * power * falloff;
                    }
                }
                else if (mode == 2)
                {
                    double power = VisualPower(actuator);
                    if (actuator is Humidifier)
                    {
                        bias += 36.0 * power * falloff;
                    }
                    else if (actuator is AirConditioner)
                    {
                        bias -= 28.0 * power * falloff;
                    }
                }
                else if (mode == 3 && actuator is LightingSystem)
                {
                    bias += 90.0 * VisualPower(actuator) * falloff;
                }
                else if (mode == 4 && actuator is FertilizerDoser doser)
                {
                    double alkali = doser.CurrentPowerRatio;
                    double acid = doser.AcidPowerRatio;
                    if (doser.TargetPowerRatio > 0)
                    {
                        alkali = Math.Max(alkali, 0.65);
                    }

                    if (doser.AcidTargetRatio > 0)
                    {
                        acid = Math.Max(acid, 0.65);
                    }

                    bias += 1.8 * (alkali - acid) * falloff;
                }
            }

            return bias;
        }

        private static double VisualPower(Actuator actuator)
        {
            double power = actuator.CurrentPowerRatio;
            if (actuator.IsOn)
            {
                power = Math.Max(power, 0.65);
            }

            return power;
        }

        private bool EnsureHeatmapSurface(int cellsX, int cellsY)
        {
            if (_heatmapBitmap != null && _heatmapBitmap.PixelWidth == cellsX && _heatmapBitmap.PixelHeight == cellsY && _heatmapPixels != null)
            {
                return true;
            }

            _heatmapBitmap = new WriteableBitmap(cellsX, cellsY, 96, 96, PixelFormats.Bgra32, null);
            _heatmapPixels = new byte[cellsX * cellsY * 4];
            if (_heatmapImage == null)
            {
                _heatmapImage = new Image
                {
                    Stretch = Stretch.Fill,
                    IsHitTestVisible = false,
                    SnapsToDevicePixels = false
                };
                RenderOptions.SetBitmapScalingMode(_heatmapImage, BitmapScalingMode.Linear);
                HeatmapCanvas.Children.Add(_heatmapImage);
            }

            _heatmapImage.Source = _heatmapBitmap;
            return true;
        }

        private void UpdateGridLines(double canvasWidth, double canvasHeight, int cellsX, int cellsY, double step, double hallWidth, double hallLength)
        {
            if (_gridShade != null
                && _gridCellsX == cellsX
                && _gridCellsY == cellsY
                && Math.Abs(_gridCanvasWidth - canvasWidth) < 0.5
                && Math.Abs(_gridCanvasHeight - canvasHeight) < 0.5)
            {
                return;
            }

            _gridCellsX = cellsX;
            _gridCellsY = cellsY;
            _gridCanvasWidth = canvasWidth;
            _gridCanvasHeight = canvasHeight;

            double scaleX = canvasWidth / hallWidth;
            double scaleY = canvasHeight / hallLength;
            double fieldWidth = cellsX * step * scaleX;
            double fieldHeight = cellsY * step * scaleY;
            var geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                for (int i = 0; i <= cellsX; i++)
                {
                    double x = i * step * scaleX;
                    context.BeginFigure(new Point(x, 0), false, false);
                    context.LineTo(new Point(x, fieldHeight), true, false);
                }

                for (int j = 0; j <= cellsY; j++)
                {
                    double y = j * step * scaleY;
                    context.BeginFigure(new Point(0, y), false, false);
                    context.LineTo(new Point(fieldWidth, y), true, false);
                }
            }

            geometry.Freeze();
            if (_gridShade == null || _gridLine == null)
            {
                _gridShade = CreateGridPath(Color.FromArgb(90, 255, 255, 255), 1.35);
                _gridLine = CreateGridPath(Color.FromArgb(120, 24, 32, 40), 0.7);
                GridCanvas.Children.Add(_gridShade);
                GridCanvas.Children.Add(_gridLine);
            }

            _gridShade.Data = geometry;
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
                stops[0] = 30;
                stops[1] = 60;
                stops[2] = 100;
                colors[0] = Color.FromRgb(224, 190, 86);
                colors[1] = Color.FromRgb(52, 186, 214);
                colors[2] = Color.FromRgb(16, 58, 186);
                return 3;
            }

            if (mode == 3)
            {
                stops[0] = 0;
                stops[1] = 28;
                stops[2] = 85;
                stops[3] = 150;
                colors[0] = Color.FromRgb(36, 44, 62);
                colors[1] = Color.FromRgb(74, 84, 108);
                colors[2] = Color.FromRgb(255, 214, 72);
                colors[3] = Color.FromRgb(255, 252, 242);
                return 4;
            }

            stops[0] = 4.5;
            stops[1] = 5.8;
            stops[2] = 7.0;
            stops[3] = 8.2;
            stops[4] = 9.5;
            colors[0] = Color.FromRgb(206, 64, 28);
            colors[1] = Color.FromRgb(232, 154, 42);
            colors[2] = Color.FromRgb(68, 174, 74);
            colors[3] = Color.FromRgb(142, 72, 186);
            colors[4] = Color.FromRgb(46, 68, 196);
            return 5;
        }

        private int BuildTemperatureRamp(double[] stops, Color[] colors)
        {
            double min = 16;
            double target = 22;
            double max = 28;
            ParameterCorridor? corridor = ActiveTemperatureCorridor();
            if (corridor != null)
            {
                min = corridor.MinValue;
                target = corridor.TargetValue;
                max = corridor.MaxValue;
            }

            double band = Math.Max(2.0, max - min);
            stops[0] = min - band;
            stops[1] = min;
            stops[2] = target;
            stops[3] = max;
            stops[4] = max + band;
            for (int i = 1; i < 5; i++)
            {
                if (stops[i] <= stops[i - 1])
                {
                    stops[i] = stops[i - 1] + 0.05;
                }
            }

            colors[0] = Color.FromRgb(28, 78, 210);
            colors[1] = Color.FromRgb(70, 196, 224);
            colors[2] = Color.FromRgb(64, 176, 78);
            colors[3] = Color.FromRgb(242, 138, 36);
            colors[4] = Color.FromRgb(204, 42, 32);
            return 5;
        }

        private ParameterCorridor? ActiveTemperatureCorridor()
        {
            var plan = _controller.ActivePlan;
            if (plan == null || _controller.CurrentDay > plan.TotalDurationDays)
            {
                return null;
            }

            return plan.GetPhaseForDay(_controller.CurrentDay).GetTempCorridor(_controller.HourOfDay);
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

            if (!string.IsNullOrEmpty(line)) TxtHistory.Text = $"{timeStamp} {line}\n\n" + TxtHistory.Text;
        }

        private void LogClimateRegime(string timeStamp, int day, int hour)
        {
            if (!TryDescribeClimate(day, hour, out string description) || _climateRegime == description) return;
            _climateRegime = description;
            TxtHistory.Text = $"{timeStamp} {description}\n\n" + TxtHistory.Text;
        }

        private bool TryDescribeClimate(out string description) => TryDescribeClimate(_controller.CurrentDay, _controller.HourOfDay, out description);

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
            _currentAlerts.Add($"• {sensorId}: {badValue:F1}");
        }

        // --- ГРАФИКА И ОБНОВЛЕНИЕ ЭКРАНА ---
        private void GreenhouseCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (_controller == null || _controller.Greenhouse == null) return;
            
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
                    TxtAlerts.Text = "ВНИМАНИЕ! ОТКЛОНЕНИЯ:\n" + string.Join("\n", _currentAlerts);
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
            GreenhouseCanvas.Children.Clear();

            double cw = GreenhouseCanvas.ActualWidth;
            double ch = GreenhouseCanvas.ActualHeight;
            if (cw == 0 || ch == 0) return;

            var gh = _controller.Greenhouse;
            double scaleX = cw / gh.Width;
            double scaleY = ch / gh.Length;

            foreach (var act in gh.GetAllActuators())
            {
                double px = act.Location.X * scaleX;
                double py = act.Location.Y * scaleY;
                DrawActuator(act, px, py);
            }

            foreach (var sensor in gh.GetAllSensors())
            {
                double px = sensor.Location.X * scaleX;
                double py = sensor.Location.Y * scaleY;
                DrawSensor(sensor, px, py);
            }
        }

        // --- МЕТОДЫ РИСОВАНИЯ ФИГУР ---
        private LinearGradientBrush GetGradient(Color top, Color bottom)
        {
            return new LinearGradientBrush(top, bottom, new Point(0, 0), new Point(0, 1));
        }

        private DropShadowEffect GetShadow(bool isGlowing = false)
        {
            if (isGlowing)
            {
                return new DropShadowEffect { Color = Colors.Red, Direction = 0, ShadowDepth = 0, BlurRadius = 20, Opacity = 1.0 };
            }
            return new DropShadowEffect { Color = Colors.Black, Direction = 315, ShadowDepth = 4, BlurRadius = 5, Opacity = 0.6 };
        }

        private void DrawSensor(Sensor sensor, double x, double y)
        {
            Shape sensorShape;
            Color textColor = Colors.Black;
            string valueTxt = Math.Round(sensor.CurrentValue, 1).ToString();

            if (sensor.Type == SensorType.Temperature)
            {
                sensorShape = new Ellipse { Width = 60, Height = 35, Fill = GetGradient(Colors.LightCyan, Colors.SkyBlue) };
                valueTxt += "°";
            }
            else if (sensor.Type == SensorType.Humidity)
            {
                sensorShape = new Ellipse { Width = 60, Height = 35, Fill = GetGradient(Color.FromRgb(70, 150, 160), Color.FromRgb(30, 100, 110)) };
                textColor = Colors.White;
                valueTxt = Math.Round(sensor.CurrentValue).ToString() + "%";
            }
            else if (sensor.Type == SensorType.Ec)
            {
                sensorShape = new Ellipse { Width = 60, Height = 35, Fill = GetGradient(Color.FromRgb(90, 100, 170), Color.FromRgb(40, 50, 110)) };
                textColor = Colors.White;
            }
            else 
            {
                sensorShape = new Ellipse { Width = 60, Height = 35, Fill = GetGradient(Color.FromRgb(200, 100, 100), Color.FromRgb(150, 50, 50)) };
                textColor = Colors.White;
            }

            sensorShape.Effect = GetShadow();
            TextBlock tb = new TextBlock { Text = valueTxt, Foreground = new SolidColorBrush(textColor), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 13, FontWeight = FontWeights.Bold };
            Grid g = new Grid { Width = 60, Height = 35 };
            g.Children.Add(sensorShape); g.Children.Add(tb);
            Canvas.SetLeft(g, x - 30); Canvas.SetTop(g, y - 17);
            GreenhouseCanvas.Children.Add(g);
        }

        private void DrawActuator(Actuator actuator, double x, double y)
        {
            FrameworkElement visual = null;
            bool on = actuator.IsOn;

            if (actuator is Heater)
            {
                Border b = new Border { Width = 120, Height = 35, CornerRadius = new CornerRadius(5), Background = GetGradient(Color.FromRgb(170, 210, 120), Color.FromRgb(120, 160, 70)), Effect = GetShadow(on) };
                b.Child = new TextBlock { Text = "обогреватель", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Black };
                visual = b;
                Canvas.SetLeft(visual, x - 60); Canvas.SetTop(visual, y - 17);
            }
            else if (actuator is AirConditioner)
            {
                Border outer = new Border { Width = 120, Height = 35, Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(100, 180, 200)), BorderThickness = new Thickness(4), CornerRadius = new CornerRadius(3), Effect = GetShadow(on) };
                Border inner = new Border { Margin = new Thickness(2), BorderBrush = new SolidColorBrush(Color.FromRgb(100, 180, 200)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2) };
                TextBlock tb = new TextBlock { Text = "кондиционер", FontSize = 12, Foreground = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                inner.Child = tb;
                outer.Child = inner;
                visual = outer;
                Canvas.SetLeft(visual, x - 60);
                Canvas.SetTop(visual, y - 17);
            }
            else if (actuator is Humidifier)
            {
                Grid g = new Grid { Width = 50, Height = 50, Effect = GetShadow(on) };
                Polygon pOuter = new Polygon { Points = new PointCollection(new[] { new Point(15, 0), new Point(35, 0), new Point(50, 45), new Point(0, 45) }), Fill = new SolidColorBrush(Color.FromRgb(130, 90, 160)) };
                Polygon pInner = new Polygon { Points = new PointCollection(new[] { new Point(18, 5), new Point(32, 5), new Point(44, 40), new Point(6, 40) }), Stroke = Brushes.White, StrokeThickness = 2 };
                g.Children.Add(pOuter); g.Children.Add(pInner); visual = g;
                Canvas.SetLeft(visual, x - 25); Canvas.SetTop(visual, y - 25);
            }
            else if (actuator is LightingSystem)
            {
                Grid g = new Grid { Width = 40, Height = 40, Effect = GetShadow(on) };
                Path rays = new Path { Data = Geometry.Parse("M 20,6 L 25,16 L 36,16 L 28,23 L 32,34 L 20,28 L 8,34 L 12,23 L 3,16 L 15,16 Z"), Fill = Brushes.Gold, Stroke = Brushes.Black, StrokeThickness = 1 };
                Ellipse core = new Ellipse { Width = 18, Height = 18, Fill = Brushes.Yellow, Stroke = Brushes.Black, StrokeThickness = 1, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                g.Children.Add(rays); g.Children.Add(core); visual = g;
                Canvas.SetLeft(visual, x - 20); Canvas.SetTop(visual, y - 20);
            }
            else if (actuator is FertilizerDoser)
            {
                Path lightning = new Path { Data = Geometry.Parse("M 15,0 L 0,15 L 10,15 L 0,35 L 25,10 L 15,10 Z"), Fill = GetGradient(Color.FromRgb(80, 40, 20), Colors.Black), Effect = GetShadow(on) };
                visual = lightning;
                Canvas.SetLeft(visual, x - 15); Canvas.SetTop(visual, y - 20);
            }

            if (visual != null)
            {
                GreenhouseCanvas.Children.Add(visual);
            }
        }
    }
}