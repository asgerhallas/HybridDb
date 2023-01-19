namespace HybridDb
{
    public static class GlobalStats
    {
        public static long NumberOfConnections { get; internal set; } = 0;
        public static long NumberOfTransaction { get; internal set; } = 0;
    }
}