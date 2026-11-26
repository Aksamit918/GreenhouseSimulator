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
        public double DiffusionCoeff { get; set; } = 2.4;
        public double MaxDiffusionStepFactor { get; set; } = 0.24;
        public double InteriorAirMix { get; set; } = 0.75;
        public double PerimeterAirMix { get; set; } = 0.35;
        public double PlumeResidenceHours { get; set; } = 0.6;
        public double PlumeRadiusFactor { get; set; } = 0.4;
        public double TMin { get; set; } = 5.0;
        public double TMax { get; set; } = 15.0;
        public double HoursPerTick { get; set; } = 1.0;
        public int SubstepsPerHour { get; set; } = 12;
        public double OutsideMinTempHour { get; set; } = 4.0;
        public double OutsideRelativeHumidity { get; set; } = 80.0;
        public double AirExchangeRate { get; set; } = 0.03;
        public int RandomSeed { get; set; } = 42;
        public double InitialTemperature { get; set; } = 22.0;
        public double InitialAbsoluteHumidity { get; set; } = 15.5;
        public double InitialLight { get; set; } = 0.0;
        public double InitialPh { get; set; } = 6.0;
        public double InitialEc { get; set; } = 1.5;
        public double EcPerPhDose { get; set; } = 0.4;
        public double EcUptakeRate { get; set; } = 0.02;
        public double MinPh { get; set; } = 0.0;
        public double MaxPh { get; set; } = 14.0;
        public double BaseTranspirationRate { get; set; } = 0.05;
        public double LightTranspirationFactor { get; set; } = 0.005;
        public double PhDriftRate { get; set; } = 0.03;
        public double PhDiffusionCoeff { get; set; } = 0.5;
        public double PhSupplyMix { get; set; } = 0.7;
        public double SolutionFlowSpeed { get; set; } = 2.0;
        public double DefaultDoserMlPerHour { get; set; } = 50.0;
        public bool UseGaussianAttenuation { get; set; } = true;
        public double PidKp { get; set; } = 0.4;
        public double PidKi { get; set; } = 0.05;
        public double PidKd { get; set; } = 0.02;
        public double MinActuatorWeight { get; set; } = Math.Exp(-0.5);
        public double OverheatMargin { get; set; } = 1.5;
        public double HumidityPercentPerDegree { get; set; } = 4.0;
        public double BaseGrowthTemp { get; set; } = 5.0;
        public double TempStressFactor { get; set; } = 0.08;
        public double HumidityStressFactor { get; set; } = 0.005;
        public double PhStressFactor { get; set; } = 0.5;
        public double LethalMinTemp { get; set; } = 0.0;
        public double LethalMaxTemp { get; set; } = 45.0;
        public double LethalMinPh { get; set; } = 3.0;
        public double LethalMaxPh { get; set; } = 9.0;
        public double UniformityPenaltyFactor { get; set; } = 2.0;
        public double SolarPeakHeat { get; set; } = 0.8;
        public double SolarPeakLight { get; set; } = 70.0;
        public double SolarNoonHour { get; set; } = 13.0;
        public double SolarDayLengthHours { get; set; } = 12.0;
        public double GrowthMarginTemp { get; set; } = 4.0;
        public double GrowthMarginHumidity { get; set; } = 12.0;
        public double GrowthMarginPh { get; set; } = 0.4;
        public double LightSaturation { get; set; } = 80.0;

        public void SaveToFile(string filePath)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(filePath, json);
        }

        public static SimulationConfig LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return new SimulationConfig();
            }

            string json = File.ReadAllText(filePath);

            return JsonSerializer.Deserialize<SimulationConfig>(json) ?? new SimulationConfig();
        }
    }
}
