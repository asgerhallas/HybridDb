namespace HybridDb
{
    public interface IColumnConfiguration
    {
        string Name { get; }
        Column Column { get; }
        object GetValue(object document);
        object SetValue(object value);
    }
}