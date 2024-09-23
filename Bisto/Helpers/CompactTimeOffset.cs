using MemoryPack;

namespace Bisto.Helpers
{
    [MemoryPackable]
    public partial struct CompactTimeOffset
    {
        private readonly uint _totalSeconds;

        
        public CompactTimeOffset(uint totalSeconds)
        {
            _totalSeconds = totalSeconds;
        }

        public CompactTimeOffset(TimeSpan span)
        {
            _totalSeconds = (uint)Math.Max(0, Math.Min(span.TotalSeconds, uint.MaxValue)); 
        }

        public TimeSpan ToTimeSpan() => TimeSpan.FromSeconds(_totalSeconds);
    }
}
