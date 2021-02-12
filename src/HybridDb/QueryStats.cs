namespace HybridDb
{
    public class QueryStats
    {
        public int RetrievedResults { get; set; }
        public int TotalResults { get; set; }
        public int FirstRowNumberOfWindow { get; set; }
        public long QueryDurationInMilliseconds { get; set; }

        public void CopyTo(QueryStats target)
        {
            target.TotalResults = TotalResults;
            target.RetrievedResults = RetrievedResults;
            target.FirstRowNumberOfWindow = FirstRowNumberOfWindow;
            target.QueryDurationInMilliseconds = QueryDurationInMilliseconds;
        }
    }
}