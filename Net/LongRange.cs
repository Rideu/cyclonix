namespace Cyclonix.Net
{
    public struct LongRange
    {
        public long Start, End;
        public LongRange(long start, long end)
        {
            Start = start;
            End = end;
        }

        public long GetLength(long src) => src - Start;
    }
}
