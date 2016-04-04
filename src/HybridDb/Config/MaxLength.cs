namespace HybridDb.Config
{
    public class MaxLength : Option<string>
    {
        public MaxLength() : this(-1) {}

        public MaxLength(int length)
        {
            Length = length;
        }

        public int Length { get; }
    }
}