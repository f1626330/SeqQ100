using System;

namespace Sequlite.Statistics
{
    /// <summary>
    /// Calculates statistics on a dataset as samples are received one-by-one
    /// Uses Welford's Method
    /// </summary>
    public class RollingStats
    {
        /* Z-values for confidence interval calculation:
         *  
         *  80%	    1.282
         *  85%	    1.440
         *  90%	    1.645
         *  95%	    1.960
         *  99%	    2.576
         *  99.5%	2.807
         *  99.9%	3.291
         */
        public double Min { get => _min; } //< The minimum sample value. Defaults to double.MaxValue if Count == 0
        public double Max { get => _max; } //< The maximum sample value. Defaults to double.MinValue if Count == 0
        public double Mean { get => _mean; } //< The sample arithmetic mean. Defaults to 0 if Count == 0
        public int Count { get => _count; } //< The number of samples that have been acquired
        public double Variance { get => _count > 1 ? _var / (_count - 1) : 0; } //< The squared deviation from the sample mean
        public double StdDev { get => _count > 1 ? Math.Sqrt(_var / (_count - 1)) : 0; } //< Standard deviation
        public double StdErr { get => _count > 0 ? StdDev / Math.Sqrt(_count) : 0; } //< Standard error of the mean
        public double ConfidenceHigh { get => (_count > 0) ? (_mean + (_z * StdDev / Math.Sqrt(_count))) : 0; } //<
        public double ConfidenceLow { get => (_count > 0) ? (_mean - (_z * StdDev / Math.Sqrt(_count))) : 0; } //<
        public double ConfidenceZ { get => _z; set => _z = value; } //<

        private double _max;
        private double _min;
        private double _mean;
        private double _var;
        private int _count;
        private double _z = 1.960;

        public RollingStats()
        {
            Reset();
        }
        public void Reset()
        {
            _min = double.MaxValue;
            _max = double.MinValue;
            _count = 0;
            _mean = 0;
            _var = 0;
        }
        public void Update(double value)
        {
            _count++;
            if (_count == 1)
            {
                _mean = value;
                _var = 0;
            }
            else
            {
                double delta = value - _mean;
                _mean += delta / _count;
                _var += delta * (value - _mean);
            }

            if (value < _min)
            {
                _min = value;
            }
            if (value > _max)
            {
                _max = value;
            }
        }
    }
}
