namespace HybridDb
{
    public class QueryStats
    {
        public int RetrievedResults { get; set; }
        public int TotalResults { get; set; }
        public long QueryDurationInMilliseconds { get; set; }

        public void CopyTo(QueryStats target)
        {
            target.TotalResults = TotalResults;
        }
    }
}