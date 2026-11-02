using GreenhouseSimulator.Core.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GreenhouseSimulator.Core.Services
{
    public class RandomProvider : IRandomProvider
    {
        private readonly Random _random;

        public RandomProvider(SimulationConfig config)
        {
            _random = new Random(config.RandomSeed);
        }

        public double GetBasicRandomVariable()
        {
            return _random.NextDouble();
        }

        public double GetNextGaussian(double mean = 0.0, double sigma = 1.0)
        {
            double z0 = Math.Sqrt(-2 * Math.Log(1 - GetBasicRandomVariable())) * Math.Cos(2 * Math.PI * GetBasicRandomVariable());

            return mean + sigma * z0;
        }
    }
}
