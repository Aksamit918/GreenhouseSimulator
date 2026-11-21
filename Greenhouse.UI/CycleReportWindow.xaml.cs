using GreenhouseSimulator.Core.Controllers;
using GreenhouseSimulator.Core.Models.Actuators;
using GreenhouseSimulator.Core.Models.Biology;
using GreenhouseSimulator.Core.Models.Environment;
using GreenhouseSimulator.Core.Models.Plans;
using GreenhouseSimulator.Core.Models.Statistics;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Text.Json;
using System.IO;

namespace Greenhouse.UI
{
    public partial class CycleReportWindow : Window
    {
        private GreenhouseController _controller;

        public CycleReportWindow(GreenhouseController controller, SimulationResult result, string? failureMessage)
        {
            InitializeComponent();
            _controller = controller;

            TxtMainTitle.Text = $"Отчет цикла: {_controller.ActivePlan?.CropName?.ToUpper() ?? "UNKNOWN"}";

            // Блок 1 (Агрономия)
            var crop = _controller.CropGrowth;
            if (result == SimulationResult.Completed && crop.AllCellsDead)
            {
                TxtStatus.Text = "СТАТУС: ЗАВЕРШЁН, ВСЁ ПОГИБЛО";
                TxtStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
            else if (result == SimulationResult.Completed && crop.DeadCellCount > 0)
            {
                TxtStatus.Text = "СТАТУС: ЗАВЕРШЁН";
                TxtStatus.Foreground = System.Windows.Media.Brushes.DarkOrange;
            }
            else if (result == SimulationResult.Completed)
            {
                TxtStatus.Text = "СТАТУС: УСПЕШНО ЗАВЕРШЕН";
            }
            else if (result == SimulationResult.Failed)
            {
                TxtStatus.Text = $"СТАТУС: ПРОВАЛЕН ({failureMessage})";
                TxtStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
            else
            {
                TxtStatus.Text = "СТАТУС: ПРЕРВАН ПОЛЬЗОВАТЕЛЕМ";
                TxtStatus.Foreground = System.Windows.Media.Brushes.Orange;
            }

            TxtYieldPercent.Text = $"Ожидаемая урожайность: {crop.YieldPotential:F1} %";
            if (crop.DeadCellCount > 0)
            {
                double share = 100.0 * crop.DeadCellCount / crop.TotalCellCount;
                var lines = new List<string>
                {
                    $"Погибло: {crop.DeadCellCount} из {crop.TotalCellCount} {CellsWord(crop.TotalCellCount)} ({share:F1} % площади)"
                };
                foreach (DeathGroup group in crop.DeathGroups)
                {
                    lines.Add($"{group.Cause}, {group.Day}-е сутки, зона ({group.X:F1}; {group.Y:F1}) м — {group.CellCount} {CellsWord(group.CellCount)}");
                }

                TxtLosses.Text = string.Join("\n", lines);
                TxtLosses.Visibility = Visibility.Visible;
            }
            TxtUniformity.Text = $"Индекс равномерности (UI): {crop.UniformityIndex:F1} %";
            TxtGdd.Text = $"Накопленные градусо-дни (GDD): {crop.AccumulatedGdd:F1}";
            TxtDli.Text = $"Накопленный свет (DLI): {crop.AccumulatedDli:F1}";
            TxtStress.Text = $"Стресс (Холод/Жара/Влага/pH): {crop.TotalStressIndex:F2}";
            if (_controller.ConflictWarnings.Count == 0)
            {
                TxtConflicts.Text = "Конфликты осушения и перегрева: не зафиксированы.";
            }
            else
            {
                TxtConflicts.Text = "Конфликты осушения и перегрева:\n" + string.Join("\n", _controller.ConflictWarnings);
            }

            var stats = _controller.Statistics;
            TxtSlaTemp.Text = $"Температура: {stats.TemperatureSlaPercent:F1} % времени в коридоре";
            TxtSlaHumidity.Text = $"Влажность: {stats.HumiditySlaPercent:F1} % времени в коридоре";
            TxtSlaPh.Text = $"pH: {stats.PhSlaPercent:F1} % времени в коридоре";
            TxtSlaLight.Text = $"Свет: {stats.LightSlaPercent:F1} % времени по расписанию";
            TxtAlertCount.Text = $"Аварийных срабатываний датчиков: {stats.TotalAlertsCount}";
            TxtStressSplit.Text = $"Холод {crop.ColdStress:F2} · Жара {crop.HeatStress:F2} · Влага {crop.HumidityStress:F2} · pH {crop.PhStress:F2}";

            double heatKwh = _controller.Greenhouse.GetAllActuators().OfType<Heater>().Sum(a => a.TotalEnergyKwh);
            double coolKwh = _controller.Greenhouse.GetAllActuators().OfType<AirConditioner>().Sum(a => a.TotalEnergyKwh);
            double lightKwh = _controller.Greenhouse.GetAllActuators().OfType<LightingSystem>().Sum(a => a.TotalEnergyKwh);
            double totalKwh = _controller.Greenhouse.GetAllActuators().Sum(a => a.TotalEnergyKwh);
            double totalWater = _controller.Greenhouse.GetAllActuators().OfType<Humidifier>().Sum(h => h.TotalWaterConsumedLiters);
            double totalReagent = _controller.Greenhouse.GetAllActuators().OfType<FertilizerDoser>().Sum(d => d.TotalReagentConsumedMl);
            double totalAcid = _controller.Greenhouse.GetAllActuators().OfType<FertilizerDoser>().Sum(d => d.TotalAcidConsumedMl);

            TxtPower.Text = $"Потреблено электроэнергии: {totalKwh:F1} кВт·ч (отопление {heatKwh:F1}, кондиционирование {coolKwh:F1}, досветка {lightKwh:F1})";
            TxtWater.Text = $"Расход очищенной воды: {totalWater:F1} л";
            TxtReagent.Text = $"Расход удобрений (pH): {totalReagent:F1} мл; кислоты: {totalAcid:F1} мл";

            PlanPhase? advicePhase = _controller.ActivePlan?.Phases.OrderBy(phase => phase.DayNumber).LastOrDefault();
            if (advicePhase == null)
            {
                TxtRecommendations.Text = "План не загружен, рекомендации по зонам недоступны.";
            }
            else
            {
                IReadOnlyList<string> advice = stats.DescribeZones(
                    _controller.Greenhouse.Grid,
                    _controller.Greenhouse.GetAllActuators(),
                    advicePhase.GetTempCorridor(12),
                    advicePhase.HumidityCorridor);
                TxtRecommendations.Text = string.Join("\n", advice);
            }

            DrawClimateChart(ChartTemperature, stats.DailyHistory, true);
            DrawClimateChart(ChartHumidity, stats.DailyHistory, false);
            ReportHeatmap.Source = BuildTemperatureBitmap(_controller.Greenhouse.Grid);
            RenderOptions.SetBitmapScalingMode(ReportHeatmap, BitmapScalingMode.Linear);
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var report = new
            {
                Crop = _controller.ActivePlan?.CropName,
                Yield = _controller.CropGrowth.YieldPotential,
                UI = _controller.CropGrowth.UniformityIndex,
                DeadCells = _controller.CropGrowth.DeadCellCount,
                Losses = _controller.CropGrowth.DeathGroups.Select(group => new
                {
                    group.Cause,
                    group.Day,
                    group.X,
                    group.Y,
                    group.CellCount
                }),
                EnergyKwh = _controller.Greenhouse.GetAllActuators().Sum(a => a.TotalEnergyKwh),
                HeatingKwh = _controller.Greenhouse.GetAllActuators().OfType<Heater>().Sum(a => a.TotalEnergyKwh),
                CoolingKwh = _controller.Greenhouse.GetAllActuators().OfType<AirConditioner>().Sum(a => a.TotalEnergyKwh),
                LightingKwh = _controller.Greenhouse.GetAllActuators().OfType<LightingSystem>().Sum(a => a.TotalEnergyKwh),
                ReagentMl = _controller.Greenhouse.GetAllActuators().OfType<FertilizerDoser>().Sum(d => d.TotalReagentConsumedMl),
                AcidMl = _controller.Greenhouse.GetAllActuators().OfType<FertilizerDoser>().Sum(d => d.TotalAcidConsumedMl),
                TemperatureSla = _controller.Statistics.TemperatureSlaPercent,
                HumiditySla = _controller.Statistics.HumiditySlaPercent,
                PhSla = _controller.Statistics.PhSlaPercent,
                LightSla = _controller.Statistics.LightSlaPercent,
                Alerts = _controller.Statistics.TotalAlertsCount,
                Days = _controller.Statistics.DailyHistory.Select(day => new
                {
                    day.DayNumber,
                    day.Mintemp,
                    day.AvgTemp,
                    day.MaxTemp,
                    day.MinHumidity,
                    day.AvgHumidity,
                    day.MaxHumidity
                }),
                Recommendations = TxtRecommendations.Text,
                Conflicts = _controller.ConflictWarnings
            };
            string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("CycleReport.json", json);
            MessageBox.Show("Отчет сохранен в папку с программой (CycleReport.json)!", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private static void DrawClimateChart(Canvas canvas, IReadOnlyList<DailyClimateRecord> days, bool temperature)
        {
            canvas.Children.Clear();
            double width = canvas.Width;
            double height = canvas.Height;
            if (days.Count == 0)
            {
                canvas.Children.Add(new TextBlock
                {
                    Text = "Суточных точек ещё нет.",
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(8)
                });
                return;
            }

            double low = double.PositiveInfinity;
            double high = double.NegativeInfinity;
            foreach (DailyClimateRecord day in days)
            {
                double sampleMin = temperature ? day.Mintemp : day.MinHumidity;
                double sampleMax = temperature ? day.MaxTemp : day.MaxHumidity;
                double corridorMin = temperature ? day.TempCorridorMin : day.HumidityCorridorMin;
                double corridorMax = temperature ? day.TempCorridorMax : day.HumidityCorridorMax;
                low = Math.Min(low, Math.Min(sampleMin, corridorMin));
                high = Math.Max(high, Math.Max(sampleMax, corridorMax));
            }

            if (high - low < 0.1)
            {
                high += 1;
                low -= 1;
            }

            double Span(int index) => days.Count == 1 ? width / 2 : 12 + index * (width - 24) / (days.Count - 1);
            double Y(double value) => 8 + (high - value) / (high - low) * (height - 16);

            for (int i = 0; i < days.Count; i++)
            {
                DailyClimateRecord day = days[i];
                double x0 = i == 0 ? 0 : (Span(i - 1) + Span(i)) / 2;
                double x1 = i == days.Count - 1 ? width : (Span(i) + Span(i + 1)) / 2;
                double corridorMin = temperature ? day.TempCorridorMin : day.HumidityCorridorMin;
                double corridorMax = temperature ? day.TempCorridorMax : day.HumidityCorridorMax;
                canvas.Children.Add(new Rectangle
                {
                    Width = Math.Max(1, x1 - x0),
                    Height = Math.Max(1, Y(corridorMin) - Y(corridorMax)),
                    Fill = new SolidColorBrush(Color.FromArgb(90, 129, 199, 132))
                });
                Canvas.SetLeft(canvas.Children[^1], x0);
                Canvas.SetTop(canvas.Children[^1], Y(corridorMax));
            }

            AddSeries(canvas, days, Span, Y, record => temperature ? record.Mintemp : record.MinHumidity, Color.FromRgb(144, 164, 174));
            AddSeries(canvas, days, Span, Y, record => temperature ? record.AvgTemp : record.AvgHumidity, Color.FromRgb(33, 33, 33));
            AddSeries(canvas, days, Span, Y, record => temperature ? record.MaxTemp : record.MaxHumidity, Color.FromRgb(183, 28, 28));
        }

        private static void AddSeries(
            Canvas canvas,
            IReadOnlyList<DailyClimateRecord> days,
            Func<int, double> span,
            Func<double, double> yOf,
            Func<DailyClimateRecord, double> valueOf,
            Color color)
        {
            var line = new Polyline
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 2
            };
            for (int i = 0; i < days.Count; i++)
            {
                line.Points.Add(new Point(span(i), yOf(valueOf(days[i]))));
            }

            canvas.Children.Add(line);
        }

        private static WriteableBitmap BuildTemperatureBitmap(EnvironmentGrid grid)
        {
            int w = grid.WidthCells;
            int h = grid.LengthCells;
            var bitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
            var pixels = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    double value = grid.GetCell(x, y).Temperature;
                    double ratio = Math.Clamp((value - 10.0) / 25.0, 0.0, 1.0);
                    Color color = ratio < 0.5
                        ? Mix(Colors.Blue, Colors.Lime, ratio * 2.0)
                        : Mix(Colors.Lime, Colors.Red, (ratio - 0.5) * 2.0);
                    int pixel = (y * w + x) * 4;
                    pixels[pixel] = color.B;
                    pixels[pixel + 1] = color.G;
                    pixels[pixel + 2] = color.R;
                    pixels[pixel + 3] = 255;
                }
            }

            bitmap.WritePixels(new Int32Rect(0, 0, w, h), pixels, w * 4, 0);
            return bitmap;
        }

        private static Color Mix(Color from, Color to, double t)
        {
            return Color.FromArgb(
                255,
                (byte)(from.R + (to.R - from.R) * t),
                (byte)(from.G + (to.G - from.G) * t),
                (byte)(from.B + (to.B - from.B) * t));
        }

        private static string CellsWord(int count)
        {
            int lastTwo = count % 100;
            int last = count % 10;
            if (lastTwo is >= 11 and <= 14)
            {
                return "клеток";
            }

            if (last == 1)
            {
                return "клетка";
            }

            if (last is >= 2 and <= 4)
            {
                return "клетки";
            }

            return "клеток";
        }
    }
}