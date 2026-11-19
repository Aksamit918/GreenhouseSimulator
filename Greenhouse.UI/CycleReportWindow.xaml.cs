using GreenhouseSimulator.Core.Controllers;
using GreenhouseSimulator.Core.Models.Actuators;
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
            if (result == SimulationResult.Completed) TxtStatus.Text = "СТАТУС: УСПЕШНО ЗАВЕРШЕН";
            else if (result == SimulationResult.Failed) { TxtStatus.Text = $"СТАТУС: ПРОВАЛЕН ({failureMessage})"; TxtStatus.Foreground = System.Windows.Media.Brushes.Red; }
            else { TxtStatus.Text = "СТАТУС: ПРЕРВАН ПОЛЬЗОВАТЕЛЕМ"; TxtStatus.Foreground = System.Windows.Media.Brushes.Orange; }

            var crop = _controller.CropGrowth;
            TxtYieldPercent.Text = $"Ожидаемая урожайность: {crop.YieldPotential:F1} %";
            TxtUniformity.Text = $"Индекс равномерности (UI): {crop.UniformityIndex:F1} %";
            TxtGdd.Text = $"Накопленные градусо-дни (GDD): {crop.AccumulatedGdd:F1}";
            TxtStress.Text = $"Стресс (Холод/Жара/Влага/pH): {crop.TotalStressIndex:F2}";

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
                EnergyKwh = _controller.Greenhouse.GetAllActuators().Sum(a => a.TotalEnergyKwh)
            };
            string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("CycleReport.json", json);
            MessageBox.Show("Отчет сохранен в папку с программой (CycleReport.json)!", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}