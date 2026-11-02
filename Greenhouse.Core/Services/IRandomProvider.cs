using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GreenhouseSimulator.Core.Services
{
    public interface IRandomProvider
    {
        double GetBasicRandomVariable();
        double GetNextGaussian(double mean = 0.0, double sigma = 1.0);
    }
}
