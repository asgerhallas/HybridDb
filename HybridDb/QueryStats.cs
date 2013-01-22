namespace HybridDb
{
    public class QueryStats
    {
        public int TotalResults { get; set; }

        public void CopyTo(QueryStats target)
        {
            target.TotalResults = TotalResults;
        }
    }
}