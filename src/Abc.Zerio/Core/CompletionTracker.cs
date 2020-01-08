using System.Collections.Generic;

namespace Abc.Zerio.Core
{
    /// <summary>
    /// Tracks completed sequence numbers.
    /// Optimized for the case when sequence numbers are completed in order.
    /// </summary>
    internal class CompletionTracker
    {
        private readonly HashSet<long> _unorderedValues = new HashSet<long>();
        private long _lastCompleted = -1;

        public int CacheSize => 1 + _unorderedValues.Count;

        public bool IsCompleted(long value)
        {
            return value <= _lastCompleted || _unorderedValues.Count != 0 && _unorderedValues.Contains(value);
        }

        public void MarketAsCompleted(long value)
        {
            if (value != _lastCompleted + 1)
            {
                _unorderedValues.Add(value);
                return;
            }
            
            _lastCompleted = value;
            while (_unorderedValues.Count > 0 && _unorderedValues.Remove(_lastCompleted + 1))
            {
                _lastCompleted++;
            }
        }
    }
}
