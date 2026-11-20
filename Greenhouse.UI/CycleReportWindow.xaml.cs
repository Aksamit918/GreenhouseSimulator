using GreenhouseSimulator.Core.Controllers;
using GreenhouseSimulator.Core.Models.Actuators;
using GreenhouseSimulator.Core.Models.Biology;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
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
            TxtStress.Text = $"Стресс (Холод/Жара/Влага/pH): {crop.TotalStressIndex:F2}";
            if (_controller.ConflictWarnings.Count == 0)
            {
                TxtConflicts.Text = "Конфликты осушения и перегрева: не зафиксированы.";
            }
            else
            {
                TxtConflicts.Text = "Конфликты осушения и перегрева:\n" + string.Join("\n", _controller.ConflictWarnings);
            }

            // Блок 3 (Экономика)
            double totalKwh = _controller.Greenhouse.GetAllActuators().Sum(a => a.TotalEnergyKwh);
            double totalWater = _controller.Greenhouse.GetAllActuators().OfType<Humidifier>().Sum(h => h.TotalWaterConsumedLiters);
            double totalReagent = _controller.Greenhouse.GetAllActuators().OfType<FertilizerDoser>().Sum(d => d.TotalReagentConsumedMl);

            TxtPower.Text = $"Потреблено электроэнергии: {totalKwh:F1} кВт*ч";
            TxtWater.Text = $"Расход очищенной воды: {totalWater:F1} л";
            TxtReagent.Text = $"Расход удобрений (pH): {totalReagent:F1} мл";
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
                Conflicts = _controller.ConflictWarnings
            };
            string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("CycleReport.json", json);
            MessageBox.Show("Отчет сохранен в папку с программой (CycleReport.json)!", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
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