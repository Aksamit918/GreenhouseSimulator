using GreenhouseSimulator.Core.Controllers;
using GreenhouseSimulator.Core.Models.Actuators;
using GreenhouseSimulator.Core.Models.Plans;
using GreenhouseSimulator.Core.Models.Sensors;
using GreenhouseSimulator.Core.Services;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
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

        // Для тепловой карты (квадратики)
        private Rectangle[,] _heatmapRects;

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

        // --- ПРОСТОЙ РЕНДЕР КВАДРАТИКАМИ (HEATMAP) ---
        private void InitHeatmap()
        {
            if (_controller == null || _controller.Greenhouse == null) return;
            
            int w = _controller.Greenhouse.Grid.WidthCells;
            int h = _controller.Greenhouse.Grid.LengthCells;
            
            _heatmapRects = new Rectangle[w, h];
            HeatmapCanvas.Children.Clear();

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var rect = new Rectangle
                    {
                        Opacity = 0.5,
                        Visibility = Visibility.Collapsed
                    };
                    _heatmapRects[x, y] = rect;
                    HeatmapCanvas.Children.Add(rect);
                }
            }
        }

        private void UpdateHeatmap()
        {
            if (_heatmapRects == null) return;

            if (HeatmapComboBox.SelectedIndex == 0)
            {
                HeatmapCanvas.Visibility = Visibility.Collapsed;
                return;
            }
            HeatmapCanvas.Visibility = Visibility.Visible;

            int mode = HeatmapComboBox.SelectedIndex;
            var grid = _controller.Greenhouse.Grid;
            int w = grid.WidthCells;
            int h = grid.LengthCells;

            // Чтобы не было ошибки деления на 0 при старте
            double canvasWidth = Math.Max(1, GreenhouseCanvas.ActualWidth);
            double canvasHeight = Math.Max(1, GreenhouseCanvas.ActualHeight);

            double cellWidth = canvasWidth / w;
            double cellHeight = canvasHeight / h;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var rect = _heatmapRects[x, y];
                    
                    rect.Width = cellWidth;
                    rect.Height = cellHeight;
                    Canvas.SetLeft(rect, x * cellWidth);
                    Canvas.SetTop(rect, y * cellHeight);
                    rect.Visibility = Visibility.Visible;

                    var cell = grid.GetCell(x, y);
                    double val = 0, min = 0, max = 1;
                    Color low = Colors.Blue, mid = Colors.Lime, high = Colors.Red;

                    if (mode == 1) { val = cell.Temperature; min = 10; max = 35; low = Colors.Blue; mid = Colors.Lime; high = Colors.Red; }
                    else if (mode == 2) { val = cell.RelativeHumidity; min = 40; max = 90; low = Colors.Yellow; mid = Colors.Cyan; high = Colors.Blue; }
                    else if (mode == 3) { val = cell.Light; min = 0; max = 2000; low = Colors.Black; mid = Colors.Yellow; high = Colors.White; }
                    else if (mode == 4) { val = cell.Ph; min = 4; max = 8; low = Colors.Red; mid = Colors.Lime; high = Colors.Purple; }

                    rect.Fill = new SolidColorBrush(InterpolateColor(val, min, max, low, mid, high));
                }
            }
        }

        private Color InterpolateColor(double val, double min, double max, Color low, Color mid, Color high)
        {
            double ratio = Math.Clamp((val - min) / (max - min), 0.0, 1.0);
            if (ratio < 0.5)
            {
                double t = ratio * 2.0;
                return Color.FromArgb(255, (byte)(low.R + (mid.R - low.R) * t), (byte)(low.G + (mid.G - low.G) * t), (byte)(low.B + (mid.B - low.B) * t));
            }
            else
            {
                double t = (ratio - 0.5) * 2.0;
                return Color.FromArgb(255, (byte)(mid.R + (high.R - mid.R) * t), (byte)(mid.G + (high.G - mid.G) * t), (byte)(mid.B + (high.B - mid.B) * t));
            }
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
            
            UpdateHeatmap();

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