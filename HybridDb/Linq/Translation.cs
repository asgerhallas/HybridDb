namespace HybridDb.Linq
{
    public class Translation
    {
        public string Select { get; set; }
        public string Where { get; set; }
        public int Take { get; set; }
        public int Skip { get; set; }
        public string OrderBy { get; set; }
    }
}