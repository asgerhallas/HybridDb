using System;

namespace HybridDb.Events
{
    // NOTE: Below warning is incorrect as Tuple implements Equal
    public class Generation : Tuple<int, int>
    {
        public Generation(int major, int minor) : base(major, minor) { }

        public int Major => Item1;
        public int Minor => Item2;

        public static Generation Max => new Generation(int.MaxValue, int.MaxValue);

        public static Generation Parse(string generation)
        {
            if (generation == null) throw new ArgumentNullException(nameof(generation));

            var split = generation.Split('.');

            if (split.Length == 1)
            {
                int major;
                if (int.TryParse(split[0], out major))
                    return new Generation(major, 0);
            }

            if (split.Length == 2)
            {
                int major;
                int minor;
                if (int.TryParse(split[0], out major) && int.TryParse(split[1], out minor))
                    return new Generation(major, minor);
            }

            throw new ArgumentException($"'{generation}' is not a valid generation.");
        }

        public static implicit operator Generation(string generation) => Parse(generation);

        public static bool operator <(Generation left, Generation right) => 
            left.Major < right.Major || left.Major == right.Major && left.Minor < right.Minor;

        public static bool operator >(Generation left, Generation right) => 
            left.Major > right.Major || left.Major == right.Major && left.Minor > right.Minor;

        public static bool operator <=(Generation left, Generation right) => !(left > right);
        public static bool operator >=(Generation left, Generation right) => !(left < right);
        public static bool operator ==(Generation left, Generation right) => Equals(right, left);
        public static bool operator !=(Generation left, Generation right) => !Equals(left, right);

        public override string ToString() => $"{Major}.{Minor}";
    }
}