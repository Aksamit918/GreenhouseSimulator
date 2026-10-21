using GreenhouseSimulator.Core.Models.Plans;
using System;
using System.Collections.Generic;
using System.IO;
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
                Directory.CreateDirectory(_directoryPath);

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public void SavePlan(GrowingPlan plan)
        {
            ArgumentNullException.ThrowIfNull(plan);
            plan.EnsureValid();

            string filePath = GetFilePath(plan.CropName);
            string json = JsonSerializer.Serialize(plan, _jsonOptions);
            File.WriteAllText(filePath, json);
        }

        public bool Contains(string cropName)
        {
            return File.Exists(GetFilePath(cropName));
        }

        public GrowingPlan LoadPlan(string cropName)
        {
            if (string.IsNullOrWhiteSpace(cropName))
                throw new ArgumentException("Crop name cannot be null or empty.", nameof(cropName));

            string filePath = GetFilePath(cropName);

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Growing plan for '{cropName}' not found.", filePath);

            string json = File.ReadAllText(filePath);
            var plan = JsonSerializer.Deserialize<GrowingPlan>(json, _jsonOptions);

            if (plan == null)
                throw new InvalidOperationException($"Failed to load growing plan from '{filePath}'.");

            plan.EnsureValid();
            return plan;
        }

        public IReadOnlyList<string> ListPlans()
        {
            var names = new List<string>();

            foreach (string filePath in Directory.GetFiles(_directoryPath, "*.json"))
            {
                string? cropName = Path.GetFileNameWithoutExtension(filePath);
                if (!string.IsNullOrWhiteSpace(cropName))
                    names.Add(cropName);
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        private string GetFilePath(string cropName)
        {
            if (string.IsNullOrWhiteSpace(cropName))
                throw new ArgumentException("Crop name cannot be null or empty.", nameof(cropName));

            string trimmed = cropName.Trim();
            if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Crop name cannot contain path or file-name characters.", nameof(cropName));

            return Path.Combine(_directoryPath, $"{trimmed}.json");
        }
    }
}
