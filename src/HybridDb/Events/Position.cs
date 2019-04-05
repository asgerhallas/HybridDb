using System;

namespace HybridDb.Events
{
    public class Position : Tuple<long, long>
    {
        public Position(long begin, long end) : base(begin, end) { }

        public long Begin => Item1;
        public long End => Item2;
    }
}