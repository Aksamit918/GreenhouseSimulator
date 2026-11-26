using GreenhouseSimulator.Core.Config;
using GreenhouseSimulator.Core.Models.Actuators;
using System;
using System.Collections.Generic;

namespace GreenhouseSimulator.Core.Models.Environment
{
    public sealed class SolutionCircuit
    {
        private readonly SimulationConfig _config;
        private readonly double[] _ph;
        private readonly double[] _ec;
        private readonly double[] _nextPh;
        private readonly double[] _nextEc;
        private readonly double[] _doseWeights;
        private readonly int _supplyCount;
        private readonly int _returnCount;
        private readonly double _segmentLength;

        public SolutionCircuit(SimulationConfig config, int supplyCount)
        {
            _config = config;
            _supplyCount = Math.Max(1, supplyCount);
            _returnCount = _supplyCount;
            _segmentLength = config.GridStep > 0.0 ? config.GridStep : 1.0;

            int count = _supplyCount + _returnCount;
            _ph = new double[count];
            _ec = new double[count];
            _nextPh = new double[count];
            _nextEc = new double[count];
            _doseWeights = new double[_supplyCount];
            Reset();
        }

        public void Reset()
        {
            for (int i = 0; i < _ph.Length; i++)
            {
                _ph[i] = _config.InitialPh;
                _ec[i] = _config.InitialEc;
            }
        }

        public double PhAt(double x, double y) => _ph[IndexAt(x, y)];

        public double EcAt(double x, double y) => _ec[IndexAt(x, y)];

        public void Advance(double dt, IReadOnlyList<Actuator> actuators)
        {
            for (int i = 0; i < _supplyCount; i++)
            {
                _ph[i] -= _config.PhDriftRate * dt;
                _ec[i] -= _config.EcUptakeRate * dt;
            }

            foreach (Actuator actuator in actuators)
            {
                if (actuator is not FertilizerDoser doser)
                {
                    continue;
                }

                InjectDose(doser, dt);
            }

            MixSupply();
            Advect(dt);
            Mix(dt);
            Clamp();
        }

        // MaxPower is the pH rate of the whole circuit; the reagent enters the supply around the doser.
        private void InjectDose(FertilizerDoser doser, double dt)
        {
            double alkali = doser.MaxPower * doser.CurrentPowerRatio * dt;
            double acid = doser.MaxPower * doser.AcidPowerRatio * dt;
            if (alkali <= 0.0 && acid <= 0.0)
            {
                return;
            }

            double totalWeight = 0.0;
            for (int i = 0; i < _supplyCount; i++)
            {
                _doseWeights[i] = doser.GetAttenuation(((i + 0.5) * _segmentLength, doser.Location.Y));
                totalWeight += _doseWeights[i];
            }

            if (totalWeight <= 0.0)
            {
                Array.Clear(_doseWeights);
                _doseWeights[SupplyIndex(doser.Location.X)] = 1.0;
                totalWeight = 1.0;
            }

            double phDose = (alkali - acid) * _ph.Length / totalWeight;
            double ecDose = (alkali + acid) * _config.EcPerPhDose * _ph.Length / totalWeight;
            for (int i = 0; i < _supplyCount; i++)
            {
                _ph[i] += phDose * _doseWeights[i];
                _ec[i] += ecDose * _doseWeights[i];
            }
        }

        private void Advect(double dt)
        {
            double shift = _segmentLength > 0.0
                ? Math.Max(0.0, _config.SolutionFlowSpeed * dt / _segmentLength)
                : 0.0;
            int whole = (int)Math.Floor(shift);
            double fraction = shift - whole;
            int count = _ph.Length;

            for (int i = 0; i < count; i++)
            {
                int upstream = Mod(i - whole, count);
                int further = Mod(upstream - 1, count);
                _nextPh[i] = (1.0 - fraction) * _ph[upstream] + fraction * _ph[further];
                _nextEc[i] = (1.0 - fraction) * _ec[upstream] + fraction * _ec[further];
            }

            Array.Copy(_nextPh, _ph, count);
            Array.Copy(_nextEc, _ec, count);
        }

        private void MixSupply()
        {
            double mix = Math.Clamp(_config.PhSupplyMix, 0.0, 1.0);
            if (mix <= 0.0 || _supplyCount <= 1)
            {
                return;
            }

            double phMean = 0.0;
            double ecMean = 0.0;
            for (int i = 0; i < _supplyCount; i++)
            {
                phMean += _ph[i];
                ecMean += _ec[i];
            }

            phMean /= _supplyCount;
            ecMean /= _supplyCount;
            for (int i = 0; i < _supplyCount; i++)
            {
                _ph[i] += mix * (phMean - _ph[i]);
                _ec[i] += mix * (ecMean - _ec[i]);
            }
        }

        private void Mix(double dt)
        {
            double mix = Math.Min(_config.PhDiffusionCoeff * dt, _config.MaxDiffusionStepFactor);
            mix = Math.Min(Math.Max(mix, 0.0), 0.999);
            if (mix <= 0.0)
            {
                return;
            }

            int count = _ph.Length;
            for (int i = 0; i < count; i++)
            {
                double neighborPh = 0.5 * (_ph[Mod(i - 1, count)] + _ph[Mod(i + 1, count)]);
                double neighborEc = 0.5 * (_ec[Mod(i - 1, count)] + _ec[Mod(i + 1, count)]);
                _nextPh[i] = _ph[i] + mix * (neighborPh - _ph[i]);
                _nextEc[i] = _ec[i] + mix * (neighborEc - _ec[i]);
            }

            Array.Copy(_nextPh, _ph, count);
            Array.Copy(_nextEc, _ec, count);
        }

        private void Clamp()
        {
            for (int i = 0; i < _ph.Length; i++)
            {
                _ph[i] = Math.Clamp(_ph[i], _config.MinPh, _config.MaxPh);
                _ec[i] = Math.Max(0.0, _ec[i]);
            }
        }

        private int IndexAt(double x, double y)
        {
            if (y <= _config.Length * 0.5)
            {
                return SupplyIndex(x);
            }

            double alongReturn = Math.Max(0.0, _config.Width - x);
            int offset = _segmentLength > 0.0 ? (int)(alongReturn / _segmentLength) : 0;
            return _supplyCount + Math.Clamp(offset, 0, _returnCount - 1);
        }

        private int SupplyIndex(double x)
        {
            int index = _segmentLength > 0.0 ? (int)(x / _segmentLength) : 0;
            return Math.Clamp(index, 0, _supplyCount - 1);
        }

        private static int Mod(int value, int length)
        {
            int remainder = value % length;
            return remainder < 0 ? remainder + length : remainder;
        }
    }
}
