namespace HybridDb.Config
{
    public class CollectionColumn : Column
    {
        public CollectionColumn(string columnName) : base(columnName, typeof(int))
        {
            Name = columnName;
        }
    }
}