using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GreenhouseSimulator.Core.Config
{
    public class SimulationConfig
    {
        public double Width { get; set; } = 10.0;
        public double Length { get; set; } = 6.0;
        public double GridStep { get; set; } = 0.5;
        public double WallInsulationCoeff { get; set; } = 0.15;
        public double DiffusionCoeff { get; set; } = 0.8;
        public double MaxDiffusionStepFactor { get; set; } = 0.24;
        public double TMin { get; set; } = 5.0;
        public double TMax { get; set; } = 15.0;
        public double HoursPerTick { get; set; } = 1.0;
        public int SubstepsPerHour { get; set; } = 12;
        public double OutsideMinTempHour { get; set; } = 4.0;
        public int RandomSeed { get; set; } = 42;
        public double InitialTemperature { get; set; } = 22.0;
        public double InitialAbsoluteHumidity { get; set; } = 15.5;
        public double InitialLight { get; set; } = 0.0;
        public double InitialPh { get; set; } = 6.0;
        public double MinPh { get; set; } = 0.0;
        public double MaxPh { get; set; } = 14.0;
        public double BaseTranspirationRate { get; set; } = 0.4;
        public double LightTranspirationFactor { get; set; } = 0.002;
        public double PhDriftRate { get; set; } = 0.03;
        public double PhDiffusionCoeff { get; set; } = 0.5;
        public double SolutionFlowSpeed { get; set; } = 0.4;
        public double DefaultDoserMlPerHour { get; set; } = 50.0;
        public bool UseGaussianAttenuation { get; set; } = true;
        public double PidKp { get; set; } = 0.4;
        public double PidKi { get; set; } = 0.05;
        public double PidKd { get; set; } = 0.02;

        public void SaveToFile(string filePath)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(filePath, json);
        }

        public static SimulationConfig LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath)) { return new SimulationConfig(); }

            string json = File.ReadAllText(filePath);

            return JsonSerializer.Deserialize<SimulationConfig>(json) ?? new SimulationConfig();
        }
    }
}
