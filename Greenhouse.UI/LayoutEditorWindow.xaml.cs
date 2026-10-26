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
        private FrameworkElement? _draggedElement;
        private Point _clickOffset;

        public LayoutEditorWindow()
        {
            InitializeComponent();
        }

        private void PaletteItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
                DragDrop.DoDragDrop(btn, btn.Tag.ToString(), DragDropEffects.Copy);
        }

        private void EditorCanvas_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.StringFormat)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private void EditorCanvas_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.StringFormat))
                return;

            string? itemType = e.Data.GetData(DataFormats.StringFormat) as string;
            if (itemType == null)
                return;

            Point dropPosition = e.GetPosition(EditorCanvas);
            FrameworkElement? visualItem = CreateVisualItem(itemType);
            if (visualItem == null)
                return;

            visualItem.MouseLeftButtonDown += PlacedItem_MouseDown;
            visualItem.Cursor = Cursors.Hand;

            Canvas.SetLeft(visualItem, dropPosition.X - 20);
            Canvas.SetTop(visualItem, dropPosition.Y - 20);
            EditorCanvas.Children.Add(visualItem);
        }

        private void PlacedItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element)
                return;

            _draggedElement = element;
            _clickOffset = e.GetPosition(element);
            element.CaptureMouse();
            e.Handled = true;
        }

        private void EditorCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedElement == null)
                return;

            Point mousePos = e.GetPosition(EditorCanvas);
            Canvas.SetLeft(_draggedElement, mousePos.X - _clickOffset.X);
            Canvas.SetTop(_draggedElement, mousePos.Y - _clickOffset.Y);
        }

        private void EditorCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggedElement == null)
                return;

            _draggedElement.ReleaseMouseCapture();
            _draggedElement = null;
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
            MessageBox.Show("Расстановка сохранена!\n\n(Данные готовы для передачи в слой Core)", "Сохранение", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
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
                    return new Path { Data = Geometry.Parse("M 15,0 L 0,15 L 10,15 L 0,35 L 25,10 L 15,10 Z"), Fill = GetGradient(Color.FromRgb(80, 40, 20), Colors.Black), Effect = GetShadow() };

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
