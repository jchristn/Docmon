namespace Docmon.App.Monitoring
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A fixed-capacity ring of numeric samples used to back a sparkline or chart. When full, the oldest
    /// sample is discarded as a new one is added.
    /// </summary>
    public sealed class MetricSeries
    {
        private readonly double[] _Buffer;
        private int _Count;
        private int _Head;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricSeries"/> class.
        /// </summary>
        /// <param name="capacity">The maximum number of samples to retain. Values below one are treated as one.</param>
        public MetricSeries(int capacity)
        {
            _Buffer = new double[Math.Max(1, capacity)];
        }

        /// <summary>
        /// Gets the most recent sample, or zero when empty.
        /// </summary>
        public double Latest
        {
            get { return _Count == 0 ? 0.0 : _Buffer[(_Head - 1 + _Buffer.Length) % _Buffer.Length]; }
        }

        /// <summary>
        /// Gets the maximum retained sample, or zero when empty.
        /// </summary>
        public double Max
        {
            get
            {
                double max = 0.0;
                for (int i = 0; i < _Count; i++)
                {
                    double value = _Buffer[(_Head - _Count + i + _Buffer.Length) % _Buffer.Length];
                    if (value > max)
                        max = value;
                }

                return max;
            }
        }

        /// <summary>
        /// Adds a sample.
        /// </summary>
        /// <param name="value">The sample value.</param>
        public void Add(double value)
        {
            _Buffer[_Head] = value;
            _Head = (_Head + 1) % _Buffer.Length;
            if (_Count < _Buffer.Length)
                _Count++;
        }

        /// <summary>
        /// Returns the retained samples, oldest first.
        /// </summary>
        /// <returns>The samples in chronological order. Never null.</returns>
        public IReadOnlyList<double> Snapshot()
        {
            List<double> result = new List<double>(_Count);
            for (int i = 0; i < _Count; i++)
                result.Add(_Buffer[(_Head - _Count + i + _Buffer.Length) % _Buffer.Length]);

            return result;
        }
    }
}
