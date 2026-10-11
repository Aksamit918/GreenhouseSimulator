using GreenhouseSimulator.Core.Models.Plans;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace GreenhouseSimulator.Core.Services
{
    public class FilePlanRepository : IPlanRepository
    {
        private readonly string _directoryPath;
        private readonly JsonSerializerOptions _jsonOptions;

        public FilePlanRepository(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                throw new ArgumentException("Directory path cannot be null or empty.", nameof(directoryPath));

            _directoryPath = directoryPath;

            if (!Directory.Exists(_directoryPath))
            {
                Directory.CreateDirectory(_directoryPath);
            }

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
        }

        public void SavePlan(GrowingPlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            string filePath = GetFilePath(plan.CropName);
            string json = JsonSerializer.Serialize(plan, _jsonOptions);

            File.WriteAllText(filePath, json);
        }

        public GrowingPlan LoadPlan(string cropName)
        {
            if (string.IsNullOrWhiteSpace(cropName))
                throw new ArgumentException("Crop name cannot be null or empty.", nameof(cropName));

            string filePath = GetFilePath(cropName);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Growing plan for '{cropName}' not found.", filePath);
            }

            string json = File.ReadAllText(filePath);
            var plan = JsonSerializer.Deserialize<GrowingPlan>(json, _jsonOptions);

            if (plan == null)
            {
                throw new InvalidOperationException($"Failed to load growing plan from '{filePath}'.");
            }

            return plan;
        }

        private string GetFilePath(string cropName)
        {
            return Path.Combine(_directoryPath, $"{cropName}.json");
        }
    }
}
