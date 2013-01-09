namespace HybridDb
{
    public class QueryStats
    {
        public int TotalRows { get; set; }

        public void CopyTo(QueryStats target)
        {
            target.TotalRows = TotalRows;
        }
    }
}