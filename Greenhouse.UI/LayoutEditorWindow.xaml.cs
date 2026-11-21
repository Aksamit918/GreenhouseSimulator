using GreenhouseModel = GreenhouseSimulator.Core.Models.Greenhouse;
using GreenhouseSimulator.Core.Models.Actuators;
using GreenhouseSimulator.Core.Models.Sensors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Greenhouse.UI
{
    public partial class LayoutEditorWindow : Window
    {
        private readonly GreenhouseModel _greenhouse;
        private FrameworkElement? _draggedElement;
        private FrameworkElement? _selectedElement;
        private Point _clickOffset;
        private bool _existingPlaced;

        // Временное хранилище для новых объектов до сохранения
        private List<object> _newDevices = new List<object>();

        public LayoutEditorWindow(GreenhouseModel greenhouse)
        {
            _greenhouse = greenhouse ?? throw new ArgumentNullException(nameof(greenhouse));
            InitializeComponent();
            Loaded += (_, _) => PlaceExistingDevices();
            EditorCanvas.SizeChanged += (_, _) => PlaceExistingDevices();
        }

        // --- DRAG AND DROP ---
        private void PaletteItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
                DragDrop.DoDragDrop(btn, new DataObject(DataFormats.StringFormat, btn.Tag.ToString()), DragDropEffects.Copy);
        }

        private void EditorCanvas_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.StringFormat) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void EditorCanvas_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.StringFormat)) return;
            string? itemType = e.Data.GetData(DataFormats.StringFormat) as string;
            if (string.IsNullOrWhiteSpace(itemType)) return;

            Point dropPosition = e.GetPosition(EditorCanvas);
            
            // СРАЗУ создаем реальный объект, чтобы можно было редактировать его свойства!
            double simX = dropPosition.X / EditorCanvas.ActualWidth * _greenhouse.Width;
            double simY = dropPosition.Y / EditorCanvas.ActualHeight * _greenhouse.Length;
            object? newDevice = InstantiateDevice(itemType, (Math.Max(0, simX), Math.Max(0, simY)));
            
            if (newDevice != null)
            {
                _newDevices.Add(newDevice);
                FrameworkElement? visualItem = CreateVisualItem(itemType);
                if (visualItem != null)
                {
                    visualItem.Tag = newDevice; // Привязываем РЕАЛЬНЫЙ объект
                    PlaceVisual(visualItem, dropPosition.X, dropPosition.Y);
                }
            }
            e.Handled = true;
        }

        // --- МЫШЬ НА ХОЛСТЕ ---
        private void PlacedItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element) return;
            SelectVisual(element);
            _draggedElement = element;
            _clickOffset = e.GetPosition(element);
            element.CaptureMouse();
            e.Handled = true;
        }

        private void EditorCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedElement == null) return;
            Point mousePos = e.GetPosition(EditorCanvas);
            Canvas.SetLeft(_draggedElement, mousePos.X - _clickOffset.X);
            Canvas.SetTop(_draggedElement, mousePos.Y - _clickOffset.Y);
        }

        private void EditorCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggedElement != null)
            {
                _draggedElement.ReleaseMouseCapture();
                _draggedElement = null;
            }
        }

        // --- ВЫДЕЛЕНИЕ И ПАНЕЛЬ СВОЙСТВ (DEV-06) ---
        private void SelectVisual(FrameworkElement visual)
        {
            if (_selectedElement != null) _selectedElement.Opacity = 1;
            _selectedElement = visual;
            visual.Opacity = 0.55;
            BtnRemoveSelected.IsEnabled = true;

            // Заполняем правую панель
            PropertiesPanel.IsEnabled = true;
            object? device = visual.Tag;
            
            ActuatorProps.Visibility = Visibility.Collapsed;
            LightProps.Visibility = Visibility.Collapsed;
            SensorProps.Visibility = Visibility.Collapsed;

            if (device is Actuator act)
            {
                TxtPropsTarget.Text = act.GetType().Name;
                ActuatorProps.Visibility = Visibility.Visible;
                PropMaxPower.Text = act.MaxPower.ToString("0.##");
                PropRadius.Text = act.EffectiveRadius.ToString("0.##");
                PropWarmup.Text = act.WarmupTimeHours.ToString("0.##");

                if (device is LightingSystem ls)
                {
                    LightProps.Visibility = Visibility.Visible;
                    PropEfficiency.Text = ls.Efficiency.ToString("0.##");
                }
            }
            else if (device is ActiveSensor sens)
            {
                TxtPropsTarget.Text = sens.Type.ToString() + " Sensor";
                SensorProps.Visibility = Visibility.Visible;
                PropNoise.Text = sens.SensorNoiseSigma.ToString("0.##");
                PropHysteresis.Text = sens.Hysteresis.ToString("0.##");
            }
        }

        private void BtnApplyProps_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedElement?.Tag == null) return;
            object device = _selectedElement.Tag;

            try
            {
                if (device is Actuator act)
                {
                    act.MaxPower = double.Parse(PropMaxPower.Text.Replace(".", ","));
                    act.EffectiveRadius = double.Parse(PropRadius.Text.Replace(".", ","));
                    act.WarmupTimeHours = double.Parse(PropWarmup.Text.Replace(".", ","));

                    if (device is LightingSystem ls)
                        ls.Efficiency = double.Parse(PropEfficiency.Text.Replace(".", ","));
                }
                else if (device is ActiveSensor sens)
                {
                    sens.SensorNoiseSigma = double.Parse(PropNoise.Text.Replace(".", ","));
                    sens.Hysteresis = double.Parse(PropHysteresis.Text.Replace(".", ","));
                }
                MessageBox.Show("Свойства обновлены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch
            {
                MessageBox.Show("Ошибка валидации. Проверьте формат чисел.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedElement != null)
            {
                if (_draggedElement == _selectedElement)
                {
                    _selectedElement.ReleaseMouseCapture();
                    _draggedElement = null;
                }
                EditorCanvas.Children.Remove(_selectedElement);
                PropertiesPanel.IsEnabled = false;
                TxtPropsTarget.Text = "Ничего не выбрано";
                _selectedElement = null;
                BtnRemoveSelected.IsEnabled = false;
            }
        }

        // --- СОХРАНЕНИЕ ---
        private void BtnSaveLayout_Click(object sender, RoutedEventArgs e)
        {
            ApplyLayout();
            MessageBox.Show("Расстановка применена к текущей теплице.", "Сохранение", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }

        private void ApplyLayout()
        {
            var keptSensors = new HashSet<Sensor>();
            var keptActuators = new HashSet<Actuator>();

            foreach (object child in EditorCanvas.Children)
            {
                if (child is not FrameworkElement element || element.Tag == null) continue;

                (double X, double Y) location = ToGreenhousePoint(element);
                
                if (element.Tag is Sensor sensor)
                {
                    sensor.MoveTo(location);
                    keptSensors.Add(sensor);
                    if (!_greenhouse.GetAllSensors().Contains(sensor)) _greenhouse.AddSensor(sensor);
                }
                else if (element.Tag is Actuator actuator)
                {
                    actuator.MoveTo(location);
                    keptActuators.Add(actuator);
                    if (!_greenhouse.GetAllActuators().Contains(actuator)) _greenhouse.AddActuator(actuator);
                }
            }

            // Удаляем то, что стерли с холста
            foreach (Sensor sensor in _greenhouse.GetAllSensors().ToList())
                if (!keptSensors.Contains(sensor)) _greenhouse.RemoveSensor(sensor);

            foreach (Actuator actuator in _greenhouse.GetAllActuators().ToList())
                if (!keptActuators.Contains(actuator)) _greenhouse.RemoveActuator(actuator);
        }

        // --- ИНИЦИАЛИЗАЦИЯ СТАРЫХ ---
        private void PlaceExistingDevices()
        {
            if (_existingPlaced || EditorCanvas.ActualWidth <= 0) return;
            _existingPlaced = true;

            foreach (Sensor sensor in _greenhouse.GetAllSensors()) PlaceModel(sensor, sensor.Location);
            foreach (Actuator actuator in _greenhouse.GetAllActuators()) PlaceModel(actuator, actuator.Location);
        }

        private void PlaceModel(object device, (double X, double Y) location)
        {
            string? kind = KindOf(device);
            if (kind == null) return;
            FrameworkElement? visual = CreateVisualItem(kind);
            if (visual == null) return;

            visual.Tag = device; // Связь
            double x = location.X / _greenhouse.Width * EditorCanvas.ActualWidth;
            double y = location.Y / _greenhouse.Length * EditorCanvas.ActualHeight;
            PlaceVisual(visual, x, y);
        }

        private void PlaceVisual(FrameworkElement visual, double centerX, double centerY)
        {
            visual.MouseLeftButtonDown += PlacedItem_MouseDown;
            visual.Cursor = Cursors.Hand;
            Canvas.SetLeft(visual, centerX - visual.Width / 2);
            Canvas.SetTop(visual, centerY - visual.Height / 2);
            EditorCanvas.Children.Add(visual);
        }

        private (double X, double Y) ToGreenhousePoint(FrameworkElement element)
        {
            double left = Canvas.GetLeft(element);
            double top = Canvas.GetTop(element);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;
            double centerX = left + element.Width / 2;
            double centerY = top + element.Height / 2;
            double x = centerX / EditorCanvas.ActualWidth * _greenhouse.Width;
            double y = centerY / EditorCanvas.ActualHeight * _greenhouse.Length;
            return (Math.Clamp(x, 0, _greenhouse.Width), Math.Clamp(y, 0, _greenhouse.Length));
        }

        // --- ФАБРИКИ ---
        private object? InstantiateDevice(string kind, (double X, double Y) location)
        {
            return kind switch
            {
                "Sensor_Temp" => new ActiveSensor(NextSensorId("temp"), SensorType.Temperature, location),
                "Sensor_Hum" => new ActiveSensor(NextSensorId("humidity"), SensorType.Humidity, location),
                "Sensor_Ph" => new ActiveSensor(NextSensorId("ph"), SensorType.Ph, location),
                "Sensor_Ec" => new ActiveSensor(NextSensorId("ec"), SensorType.Ec, location),
                "Actuator_Heater" => new Heater(location),
                "Actuator_AC" => new AirConditioner(location),
                "Actuator_Humidifier" => new Humidifier(location),
                "Actuator_Doser" => new FertilizerDoser(location),
                "Actuator_Light" => new LightingSystem(location),
                _ => null
            };
        }

        private string NextSensorId(string prefix)
        {
            var ids = _greenhouse.GetAllSensors().Select(s => s.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            int number = 1;
            while (ids.Contains($"{prefix}-{number}")) number++;
            return $"{prefix}-{number}";
        }

        private static string? KindOf(object device)
        {
            return device switch
            {
                Sensor { Type: SensorType.Temperature } => "Sensor_Temp",
                Sensor { Type: SensorType.Humidity } => "Sensor_Hum",
                Sensor { Type: SensorType.Ph } => "Sensor_Ph",
                Sensor { Type: SensorType.Ec } => "Sensor_Ec",
                Heater => "Actuator_Heater",
                AirConditioner => "Actuator_AC",
                Humidifier => "Actuator_Humidifier",
                FertilizerDoser => "Actuator_Doser",
                LightingSystem => "Actuator_Light",
                _ => null
            };
        }

        private FrameworkElement? CreateVisualItem(string type)
        {
            switch (type)
            {
                case "Sensor_Temp": return DrawSensor("T°", Colors.LightCyan, Colors.SkyBlue, true);
                case "Sensor_Hum": return DrawSensor("H%", Color.FromRgb(70, 150, 160), Color.FromRgb(30, 100, 110), false);
                case "Sensor_Ph": return DrawSensor("pH", Color.FromRgb(200, 100, 100), Color.FromRgb(150, 50, 50), false);
                case "Sensor_Ec": return DrawSensor("EC", Color.FromRgb(90, 100, 170), Color.FromRgb(40, 50, 110), false);
                case "Actuator_Heater":
                    Border b = new Border { Width = 90, Height = 30, CornerRadius = new CornerRadius(5), Background = GetGradient(Color.FromRgb(170, 210, 120), Color.FromRgb(120, 160, 70)), Effect = GetShadow() };
                    b.Child = new TextBlock { Text = "Обогрев", FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Black };
                    return b;
                case "Actuator_AC":
                    Border outer = new Border { Width = 30, Height = 90, Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(100, 180, 200)), BorderThickness = new Thickness(3), CornerRadius = new CornerRadius(3), Effect = GetShadow() };
                    TextBlock tb = new TextBlock { Text = "Кондей", FontSize = 11, Foreground = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, LayoutTransform = new RotateTransform(-90) };
                    outer.Child = tb; return outer;
                case "Actuator_Humidifier":
                    Grid gh = new Grid { Width = 40, Height = 40, Effect = GetShadow() };
                    gh.Children.Add(new Polygon { Points = new PointCollection(new[] { new Point(10, 0), new Point(30, 0), new Point(40, 35), new Point(0, 35) }), Fill = new SolidColorBrush(Color.FromRgb(130, 90, 160)) });
                    return gh;
                case "Actuator_Doser":
                    return new Path { Width = 25, Height = 35, Data = Geometry.Parse("M 15,0 L 0,15 L 10,15 L 0,35 L 25,10 L 15,10 Z"), Fill = GetGradient(Color.FromRgb(80, 40, 20), Colors.Black), Effect = GetShadow() };
                case "Actuator_Light":
                    Grid gl = new Grid { Width = 40, Height = 40, Effect = GetShadow() };
                    gl.Children.Add(new Path { Data = Geometry.Parse("M 20,6 L 25,16 L 36,16 L 28,23 L 32,34 L 20,28 L 8,34 L 12,23 L 3,16 L 15,16 Z"), Fill = Brushes.Gold, Stroke = Brushes.Black });
                    gl.Children.Add(new Ellipse { Width = 18, Height = 18, Fill = Brushes.Yellow, Stroke = Brushes.Black });
                    return gl;
            }
            return null;
        }

        private FrameworkElement DrawSensor(string text, Color top, Color bot, bool darkText)
        {
            Grid g = new Grid { Width = 40, Height = 40 };
            g.Children.Add(new Ellipse { Fill = GetGradient(top, bot), Effect = GetShadow() });
            g.Children.Add(new TextBlock { Text = text, Foreground = new SolidColorBrush(darkText ? Colors.Black : Colors.White), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 12, FontWeight = FontWeights.Bold });
            return g;
        }

        private LinearGradientBrush GetGradient(Color t, Color b) => new LinearGradientBrush(t, b, new Point(0, 0), new Point(0, 1));
        private DropShadowEffect GetShadow() => new DropShadowEffect { Color = Colors.Black, Direction = 315, ShadowDepth = 3, BlurRadius = 4, Opacity = 0.5 };
        private void BtnHelp_Click(object sender, RoutedEventArgs e) => OverlayHelp.Visibility = Visibility.Visible;
        private void BtnCloseHelp_Click(object sender, RoutedEventArgs e) => OverlayHelp.Visibility = Visibility.Collapsed;
    }
}