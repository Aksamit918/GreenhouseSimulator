using GreenhouseSimulator.Core.Models.Plans;

namespace GreenhouseSimulator.Core.Models.Statistics
{
    public class SimulationStatistics
    {
        public int TotalTicks { get; private set; }
        public int TotalAlertsCount { get; private set; }
        private int _tempOkTicks;
        private int _humidityOkTicks;
        private int _phOkTicks;
        private int _lightOkTicks;

        public double TemperatureSlaPercent => Percent(_tempOkTicks);
        public double HumiditySlaPercent => Percent(_humidityOkTicks);
        public double PhSlaPercent => Percent(_phOkTicks);
        public double LightSlaPercent => Percent(_lightOkTicks);

        public SimulationStatistics()
        {
            Reset();
        }

        public void Reset()
        {
            TotalTicks = 0;
            TotalAlertsCount = 0;
            _tempOkTicks = 0;
            _humidityOkTicks = 0;
            _phOkTicks = 0;
            _lightOkTicks = 0;
        }

        public void RecordAlert()
        {
            TotalAlertsCount++;
        }

        public void RecordTick(
            double temperature,
            double humidity,
            double ph,
            double light,
            ParameterCorridor temperatureCorridor,
            ParameterCorridor humidityCorridor,
            ParameterCorridor phCorridor,
            bool lightScheduled)
        {
            ArgumentNullException.ThrowIfNull(temperatureCorridor);
            ArgumentNullException.ThrowIfNull(humidityCorridor);
            ArgumentNullException.ThrowIfNull(phCorridor);

            TotalTicks++;
            if (temperatureCorridor.IsWithinCorridor(temperature))
            {
                _tempOkTicks++;
            }

            if (humidityCorridor.IsWithinCorridor(humidity))
            {
                _humidityOkTicks++;
            }

            if (phCorridor.IsWithinCorridor(ph))
            {
                _phOkTicks++;
            }

            bool lightOk = lightScheduled ? light > 0.0 : light <= 0.0;
            if (lightOk)
            {
                _lightOkTicks++;
            }
        }

        private double Percent(int okTicks)
        {
            return TotalTicks > 0 ? okTicks * 100.0 / TotalTicks : 0.0;
        }
    }
}
