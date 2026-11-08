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

        public LayoutEditorWindow(GreenhouseModel greenhouse)
        {
            _greenhouse = greenhouse ?? throw new ArgumentNullException(nameof(greenhouse));
            InitializeComponent();
            Loaded += (_, _) => PlaceExistingDevices();
            EditorCanvas.SizeChanged += (_, _) => PlaceExistingDevices();
        }

        private void PaletteItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Button btn || btn.Tag == null)
            {
                return;
            }

            var data = new DataObject(DataFormats.StringFormat, btn.Tag.ToString());
            DragDrop.DoDragDrop(btn, data, DragDropEffects.Copy);
        }

        private void EditorCanvas_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.StringFormat)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
            e.Handled = true;
        }

        private void EditorCanvas_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                return;
            }

            string? itemType = e.Data.GetData(DataFormats.StringFormat) as string;
            if (string.IsNullOrWhiteSpace(itemType))
            {
                return;
            }

            Point dropPosition = e.GetPosition(EditorCanvas);
            FrameworkElement? visualItem = CreateVisualItem(itemType);
            if (visualItem == null)
            {
                return;
            }

            visualItem.Tag = itemType;
            PlaceVisual(visualItem, dropPosition.X, dropPosition.Y);
            e.Handled = true;
        }

        private void PlacedItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element)
            {
                return;
            }

            SelectVisual(element);
            _draggedElement = element;
            _clickOffset = e.GetPosition(element);
            element.CaptureMouse();
            e.Handled = true;
        }

        private void EditorCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedElement == null)
            {
                return;
            }

            Point mousePos = e.GetPosition(EditorCanvas);
            Canvas.SetLeft(_draggedElement, mousePos.X - _clickOffset.X);
            Canvas.SetTop(_draggedElement, mousePos.Y - _clickOffset.Y);
        }

        private void EditorCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggedElement == null)
            {
                return;
            }

            _draggedElement.ReleaseMouseCapture();
            _draggedElement = null;
        }

        private void BtnRemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedElement != null)
            {
                RemoveVisual(_selectedElement);
            }
        }

        private void BtnHelp_Click(object sender, RoutedEventArgs e)
        {
            OverlayHelp.Visibility = Visibility.Visible;
        }

        private void BtnCloseHelp_Click(object sender, RoutedEventArgs e)
        {
            OverlayHelp.Visibility = Visibility.Collapsed;
        }

        private void BtnSaveLayout_Click(object sender, RoutedEventArgs e)
        {
            if (EditorCanvas.ActualWidth <= 0 || EditorCanvas.ActualHeight <= 0)
            {
                MessageBox.Show("План ещё не готов к сохранению.", "Сохранение", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ApplyLayout();
            MessageBox.Show("Расстановка применена к текущей теплице.", "Сохранение", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }

        private void PlaceExistingDevices()
        {
            if (_existingPlaced || EditorCanvas.ActualWidth <= 0 || EditorCanvas.ActualHeight <= 0)
            {
                return;
            }

            _existingPlaced = true;

            foreach (Sensor sensor in _greenhouse.GetAllSensors())
            {
                PlaceModel(sensor, sensor.Location);
            }

            foreach (Actuator actuator in _greenhouse.GetAllActuators())
            {
                PlaceModel(actuator, actuator.Location);
            }
        }

        private void PlaceModel(object device, (double X, double Y) location)
        {
            string? kind = KindOf(device);
            if (kind == null)
            {
                return;
            }

            FrameworkElement? visual = CreateVisualItem(kind);
            if (visual == null)
            {
                return;
            }

            visual.Tag = device;
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

        private void SelectVisual(FrameworkElement visual)
        {
            if (_selectedElement != null)
            {
                _selectedElement.Opacity = 1;
            }

            _selectedElement = visual;
            visual.Opacity = 0.55;
            BtnRemoveSelected.IsEnabled = true;
        }

        private void RemoveVisual(FrameworkElement visual)
        {
            if (_draggedElement == visual)
            {
                visual.ReleaseMouseCapture();
                _draggedElement = null;
            }

            EditorCanvas.Children.Remove(visual);
            if (_selectedElement != visual)
            {
                return;
            }

            _selectedElement = null;
            BtnRemoveSelected.IsEnabled = false;
        }

        private void ApplyLayout()
        {
            var keptSensors = new HashSet<Sensor>();
            var keptActuators = new HashSet<Actuator>();

            foreach (object child in EditorCanvas.Children)
            {
                if (child is not FrameworkElement element || element.Tag == null)
                {
                    continue;
                }

                (double X, double Y) location = ToGreenhousePoint(element);
                switch (element.Tag)
                {
                    case Sensor sensor:
                        sensor.MoveTo(location);
                        keptSensors.Add(sensor);
                        break;
                    case Actuator actuator:
                        actuator.MoveTo(location);
                        keptActuators.Add(actuator);
                        break;
                    case string kind:
                        Remember(AddDevice(kind, location), keptSensors, keptActuators);
                        break;
                }
            }

            foreach (Sensor sensor in _greenhouse.GetAllSensors())
            {
                if (!keptSensors.Contains(sensor))
                {
                    _greenhouse.RemoveSensor(sensor);
                }
            }

            foreach (Actuator actuator in _greenhouse.GetAllActuators())
            {
                if (!keptActuators.Contains(actuator))
                {
                    _greenhouse.RemoveActuator(actuator);
                }
            }
        }

        private (double X, double Y) ToGreenhousePoint(FrameworkElement element)
        {
            double left = Canvas.GetLeft(element);
            double top = Canvas.GetTop(element);
            if (double.IsNaN(left))
            {
                left = 0;
            }
            if (double.IsNaN(top))
            {
                top = 0;
            }

            double centerX = left + element.Width / 2;
            double centerY = top + element.Height / 2;
            double x = centerX / EditorCanvas.ActualWidth * _greenhouse.Width;
            double y = centerY / EditorCanvas.ActualHeight * _greenhouse.Length;
            return (
                Math.Clamp(x, 0, _greenhouse.Width),
                Math.Clamp(y, 0, _greenhouse.Length));
        }

        private static void Remember(object? device, HashSet<Sensor> sensors, HashSet<Actuator> actuators)
        {
            switch (device)
            {
                case Sensor sensor:
                    sensors.Add(sensor);
                    break;
                case Actuator actuator:
                    actuators.Add(actuator);
                    break;
            }
        }

        private object? AddDevice(string kind, (double X, double Y) location)
        {
            switch (kind)
            {
                case "Sensor_Temp":
                    var temperature = new ActiveSensor(NextSensorId("temp"), SensorType.Temperature, location);
                    _greenhouse.AddSensor(temperature);
                    return temperature;
                case "Sensor_Hum":
                    var humidity = new ActiveSensor(NextSensorId("humidity"), SensorType.Humidity, location);
                    _greenhouse.AddSensor(humidity);
                    return humidity;
                case "Sensor_Ph":
                    var ph = new ActiveSensor(NextSensorId("ph"), SensorType.Ph, location);
                    _greenhouse.AddSensor(ph);
                    return ph;
                case "Actuator_Heater":
                    var heater = new Heater(location);
                    _greenhouse.AddActuator(heater);
                    return heater;
                case "Actuator_AC":
                    var conditioner = new AirConditioner(location);
                    _greenhouse.AddActuator(conditioner);
                    return conditioner;
                case "Actuator_Humidifier":
                    var humidifier = new Humidifier(location);
                    _greenhouse.AddActuator(humidifier);
                    return humidifier;
                case "Actuator_Doser":
                    var doser = new FertilizerDoser(location);
                    _greenhouse.AddActuator(doser);
                    return doser;
                case "Actuator_Light":
                    var lamp = new LightingSystem(location);
                    _greenhouse.AddActuator(lamp);
                    return lamp;
            }

            return null;
        }

        private string NextSensorId(string prefix)
        {
            var ids = _greenhouse.GetAllSensors().Select(sensor => sensor.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            int number = 1;
            string id;
            do
            {
                id = $"{prefix}-{number}";
                number++;
            }
            while (ids.Contains(id));

            return id;
        }

        private static string? KindOf(object device)
        {
            return device switch
            {
                Sensor { Type: SensorType.Temperature } => "Sensor_Temp",
                Sensor { Type: SensorType.Humidity } => "Sensor_Hum",
                Sensor { Type: SensorType.Ph } => "Sensor_Ph",
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
                case "Sensor_Temp": return DrawSensor("25°", Colors.LightCyan, Colors.SkyBlue, true);
                case "Sensor_Hum": return DrawSensor("60%", Color.FromRgb(70, 150, 160), Color.FromRgb(30, 100, 110), false);
                case "Sensor_Ph": return DrawSensor("6.0", Color.FromRgb(200, 100, 100), Color.FromRgb(150, 50, 50), false);

                case "Actuator_Heater":
                    Border heater = new Border { Width = 120, Height = 35, CornerRadius = new CornerRadius(5), Background = GetGradient(Color.FromRgb(170, 210, 120), Color.FromRgb(120, 160, 70)), Effect = GetShadow() };
                    heater.Child = new TextBlock { Text = "обогреватель", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Black };
                    return heater;

                case "Actuator_AC":
                    Border outer = new Border { Width = 120, Height = 35, Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(100, 180, 200)), BorderThickness = new Thickness(4), CornerRadius = new CornerRadius(3), Effect = GetShadow() };
                    Border inner = new Border { Margin = new Thickness(2), BorderBrush = new SolidColorBrush(Color.FromRgb(100, 180, 200)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2) };
                    TextBlock label = new TextBlock { Text = "кондиционер", FontSize = 12, Foreground = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    inner.Child = label;
                    outer.Child = inner;
                    return outer;

                case "Actuator_Humidifier":
                    Grid humidifier = new Grid { Width = 40, Height = 40, Effect = GetShadow() };
                    Polygon outerShape = new Polygon { Points = new PointCollection(new[] { new Point(10, 0), new Point(30, 0), new Point(40, 35), new Point(0, 35) }), Fill = new SolidColorBrush(Color.FromRgb(130, 90, 160)) };
                    Polygon innerShape = new Polygon { Points = new PointCollection(new[] { new Point(13, 5), new Point(27, 5), new Point(35, 30), new Point(5, 30) }), Stroke = Brushes.White, StrokeThickness = 2 };
                    humidifier.Children.Add(outerShape);
                    humidifier.Children.Add(innerShape);
                    return humidifier;

                case "Actuator_Doser":
                    return new Path { Width = 25, Height = 35, Data = Geometry.Parse("M 15,0 L 0,15 L 10,15 L 0,35 L 25,10 L 15,10 Z"), Fill = GetGradient(Color.FromRgb(80, 40, 20), Colors.Black), Effect = GetShadow() };

                case "Actuator_Light":
                    Grid lamp = new Grid { Width = 40, Height = 40, Effect = GetShadow() };
                    Path rays = new Path { Data = Geometry.Parse("M 20,6 L 25,16 L 36,16 L 28,23 L 32,34 L 20,28 L 8,34 L 12,23 L 3,16 L 15,16 Z"), Fill = Brushes.Gold, Stroke = Brushes.Black, StrokeThickness = 1 };
                    Ellipse core = new Ellipse { Width = 18, Height = 18, Fill = Brushes.Yellow, Stroke = Brushes.Black, StrokeThickness = 1, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    lamp.Children.Add(rays);
                    lamp.Children.Add(core);
                    return lamp;
            }

            return null;
        }

        private FrameworkElement DrawSensor(string text, Color top, Color bottom, bool darkText)
        {
            Ellipse ellipse = new Ellipse { Width = 60, Height = 35, Fill = GetGradient(top, bottom), Effect = GetShadow() };
            return WrapWithText(ellipse, text, darkText ? Colors.Black : Colors.White);
        }

        private FrameworkElement WrapWithText(Shape shape, string text, Color textColor)
        {
            Grid grid = new Grid { Width = 60, Height = 35 };
            TextBlock label = new TextBlock { Text = text, Foreground = new SolidColorBrush(textColor), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 12, FontWeight = FontWeights.Bold };
            grid.Children.Add(shape);
            grid.Children.Add(label);
            return grid;
        }

        private LinearGradientBrush GetGradient(Color top, Color bottom) => new LinearGradientBrush(top, bottom, new Point(0, 0), new Point(0, 1));

        private DropShadowEffect GetShadow() => new DropShadowEffect { Color = Colors.Black, Direction = 315, ShadowDepth = 3, BlurRadius = 4, Opacity = 0.5 };
    }
}
