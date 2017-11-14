namespace HybridDb
{
    public class QueryResult<T>
    {
        public QueryResult(T data, string discriminator)
        {
            Data = data;
            Discriminator = discriminator;
        }

        public T Data { get; }
        public string Discriminator { get; }
    }
}