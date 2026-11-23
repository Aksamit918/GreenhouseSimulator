using GreenhouseSimulator.Core.Controllers;
using GreenhouseSimulator.Core.Models.Biology;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Greenhouse.UI
{
    public partial class CycleReportWindow : Window
    {
        public CycleReportWindow(GreenhouseController controller, SimulationResult result, string? failureMessage)
        {
            InitializeComponent();

            TxtMainTitle.Text = $"Отчет цикла: {controller.ActivePlan?.CropName?.ToUpper() ?? "UNKNOWN"}";

            // Блок 1 (Агрономия)
            var crop = controller.CropGrowth;
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
            if (controller.ConflictWarnings.Count == 0)
            {
                TxtConflicts.Text = "Конфликты осушения и перегрева: не зафиксированы.";
            }
            else
            {
                TxtConflicts.Text = "Конфликты осушения и перегрева:\n" + string.Join("\n", controller.ConflictWarnings);
            }

            var stats = controller.Statistics;
            TxtSlaTemp.Text = $"Температура: {stats.TemperatureSlaPercent:F1} % времени в коридоре";
            TxtSlaHumidity.Text = $"Влажность: {stats.HumiditySlaPercent:F1} % времени в коридоре";
            TxtSlaPh.Text = $"pH: {stats.PhSlaPercent:F1} % времени в коридоре";
            TxtSlaLight.Text = $"Свет: {stats.LightSlaPercent:F1} % времени по расписанию";
            TxtAlertCount.Text = $"Аварийных срабатываний датчиков: {stats.TotalAlertsCount}";
            TxtStressSplit.Text = $"Холод {crop.ColdStress:F2} · Жара {crop.HeatStress:F2} · Влага {crop.HumidityStress:F2} · pH {crop.PhStress:F2}";
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

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