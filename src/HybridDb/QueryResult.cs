namespace HybridDb
{
    public class QueryResult<T>
    {
        public QueryResult(T data, string discriminator) : this(data, discriminator, new byte[8]) { }

        public QueryResult(T data, string discriminator, byte[] rowVersion)
        {
            Data = data;
            Discriminator = discriminator;
            RowVersion = rowVersion;
        }

        public T Data { get; }
        public string Discriminator { get; }
        public byte[] RowVersion { get; }
        public Operation LastOperation { get; }
    }

    public enum Operation
    {
        Inserted,
        Updated,
        Deleted
    }
}