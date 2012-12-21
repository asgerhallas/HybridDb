namespace HybridDb.Schema
{
    public interface IColumn 
    {
        string Name { get; }
        Column Column { get; }
        object Serialize(object value);
    }
}