using GreenhouseSimulator.Core.Controllers;
using GreenhouseSimulator.Core.Models.Actuators;
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
        private Dictionary<string, bool> _actuatorStates = new Dictionary<string, bool>();

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

                var repo = new FilePlanRepository(plansDir);
                PlansComboBox.ItemsSource = repo.ListPlans();
                if (PlansComboBox.Items.Count > 0) PlansComboBox.SelectedIndex = 0;

                _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _timer.Tick += Timer_Tick;

                UpdateUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации: {ex.Message}");
            }
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (PlansComboBox.SelectedItem == null) return;
            try
            {
                _controller.StartCycle(PlansComboBox.SelectedItem.ToString());
                _timer.Start();

                BtnStart.IsEnabled = false; PlansComboBox.IsEnabled = false; BtnStop.IsEnabled = true;
                TxtStatus.Text = "Статус: Выращиваем...";
                
                _currentAlerts.Clear();
                TxtHistory.Text = "--- Цикл запущен ---\n";
                _actuatorStates.Clear();
                
                UpdateUI();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            _controller.StopCycle();
            _timer.Stop();
            BtnStart.IsEnabled = true; PlansComboBox.IsEnabled = true; BtnStop.IsEnabled = false;
            TxtStatus.Text = "Статус: Остановлено";
            TxtHeader.Text = "Ожидание запуска...";
            UpdateUI();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!_controller.IsRunning) { BtnStop_Click(null, null); return; }

            _currentAlerts.Clear();
            _controller.SimulationTick(); 
            
            CheckActuatorChanges();
            
            UpdateUI(); 
        }

        // ОБНОВЛЕННЫЙ МЕТОД С ПРИЧИНАМИ
        private void CheckActuatorChanges()
        {
            string timeStamp = $"[День {_controller.CurrentDay}, {_controller.HourOfDay}:00]";
            var gh = _controller.Greenhouse;

            foreach (var act in gh.GetAllActuators())
            {
                string actName = act.GetType().Name;
                bool wasOn = _actuatorStates.ContainsKey(actName) && _actuatorStates[actName];

                if (wasOn != act.IsOn)
                {
                    string logLine = "";
                    if (act.IsOn)
                    {
                        // ПРИБОР ВКЛЮЧАЕТСЯ - пишем причину (текущие значения датчика)
                        if (act is Heater) 
                            logLine = $"Для повышения температуры – выключить кондиционеры и включить обогреватели. (Зафиксировано: {gh.CurrentTemperature:F1}°C)";
                        else if (act is AirConditioner) 
                            logLine = $"Для понижения температуры – включить кондиционеры и выключить обогреватели. (Зафиксировано: {gh.CurrentTemperature:F1}°C)";
                        else if (act is Humidifier) 
                            logLine = $"Для повышения влажности – выключить обогреватели и включить увлажнители. (Зафиксировано: {gh.CurrentHumidity:F1}%)";
                        else if (act is FertilizerDoser) 
                            logLine = $"Для повышения pH – активировать дозаторы удобрений. (Зафиксировано: pH {gh.CurrentPh:F2})";
                    }
                    else
                    {
                        // ПРИБОР ВЫКЛЮЧАЕТСЯ
                        if (act is FertilizerDoser) 
                            logLine = $"При достижении заданного значения - выключить. (Текущий pH: {gh.CurrentPh:F2})";
                        else if (act is Heater) 
                            logLine = $"Температура стабилизировалась ({gh.CurrentTemperature:F1}°C) - обогреватель выключен.";
                        else if (act is AirConditioner) 
                            logLine = $"Температура стабилизировалась ({gh.CurrentTemperature:F1}°C) - кондиционер выключен.";
                        else if (act is Humidifier) 
                            logLine = $"Влажность стабилизировалась ({gh.CurrentHumidity:F1}%) - увлажнитель выключен.";
                    }

                    if (!string.IsNullOrEmpty(logLine))
                    {
                        TxtHistory.Text = $"{timeStamp} {logLine}\n\n" + TxtHistory.Text;
                    }
                    
                    _actuatorStates[actName] = act.IsOn;
                }
            }
        }

        private void OnAlertTriggered(string sensorId, double badValue)
        {
            _currentAlerts.Add($"• {sensorId}: {badValue:F1}");
        }

        private void GreenhouseCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (_controller == null || _controller.Greenhouse == null) return;

            if (_controller.IsRunning)
            {
                if (_controller.StartDate.HasValue)
                {
                    int days = _controller.CurrentDay - 1;
                    int hours = _controller.HourOfDay;
                    string crop = _controller.ActivePlan?.CropName ?? "Неизвестно";
                    TxtHeader.Text = $"{crop}, начало цикла – {_controller.StartDate.Value:dd.MM.yyyy}, прошло: {days} суток, {hours} часов";
                }

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
                double px = act.Location.Item1 * scaleX;
                double py = act.Location.Item2 * scaleY;
                DrawActuator(act, px, py);
            }

            foreach (var sensor in gh.GetAllSensors())
            {
                double px = sensor.Location.Item1 * scaleX;
                double py = sensor.Location.Item2 * scaleY;
                DrawSensor(sensor, px, py);
            }
        }

        private LinearGradientBrush GetGradient(Color top, Color bottom)
        {
            return new LinearGradientBrush(top, bottom, new Point(0, 0), new Point(0, 1));
        }

        private DropShadowEffect GetShadow(bool isGlowing = false)
        {
            if (isGlowing)
                return new DropShadowEffect { Color = Colors.Red, Direction = 0, ShadowDepth = 0, BlurRadius = 20, Opacity = 1.0 };
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
            else if (sensor.Type == SensorType.Light)
            {
                sensorShape = new Rectangle { Width = 60, Height = 35, RadiusX = 10, RadiusY = 10, Fill = GetGradient(Colors.LightYellow, Colors.Gold) };
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

        private void DrawActuator(IActuator actuator, double x, double y)
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
                Border outer = new Border { Width = 40, Height = 140, Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(100, 180, 200)), BorderThickness = new Thickness(4), CornerRadius = new CornerRadius(3), Effect = GetShadow(on) };
                Border inner = new Border { Margin = new Thickness(2), BorderBrush = new SolidColorBrush(Color.FromRgb(100, 180, 200)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2) };
                TextBlock tb = new TextBlock { Text = "кондиционер", FontSize = 12, Foreground = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                tb.LayoutTransform = new RotateTransform(-90);
                inner.Child = tb; outer.Child = inner; visual = outer;
                Canvas.SetLeft(visual, x - 20); Canvas.SetTop(visual, y - 70);
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
                Path rays = new Path { Data = Geometry.Parse("M 20,0 L 25,12 L 38,12 L 28,20 L 32,32 L 20,25 L 8,32 L 12,20 L 2,12 L 15,12 Z"), Fill = Brushes.Gold, Stroke = Brushes.Black, StrokeThickness = 1 };
                Ellipse core = new Ellipse { Width = 20, Height = 20, Fill = Brushes.Yellow, Stroke = Brushes.Black, StrokeThickness = 1, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                g.Children.Add(rays); g.Children.Add(core); visual = g;
                Canvas.SetLeft(visual, x - 20); Canvas.SetTop(visual, y - 20);
            }
            else if (actuator is FertilizerDoser)
            {
                Path lightning = new Path { Data = Geometry.Parse("M 20,0 L 5,20 L 15,20 L 5,40 L 30,15 L 20,15 Z"), Fill = GetGradient(Color.FromRgb(80, 40, 20), Colors.Black), Effect = GetShadow(on) };
                visual = lightning;
                Canvas.SetLeft(visual, x - 15); Canvas.SetTop(visual, y - 20);
            }

            if (visual != null) GreenhouseCanvas.Children.Add(visual);
        }
    }
}