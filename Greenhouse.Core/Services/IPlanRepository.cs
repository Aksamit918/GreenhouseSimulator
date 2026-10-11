using GreenhouseSimulator.Core.Models.Plans;
using System;
using System.Collections.Generic;
using System.Text;

namespace GreenhouseSimulator.Core.Services
{
    public interface IPlanRepository
    {
        void SavePlan(GrowingPlan plan);
        GrowingPlan LoadPlan(string cropName);
    }
}
