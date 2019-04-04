namespace HybridDb
{
    public class QueryResult<T>
    {
        public QueryResult(T data, string discriminator, Operation lastOperation, byte[] rowVersion)
        {
            Data = data;
            Discriminator = discriminator;
            RowVersion = rowVersion;
            LastOperation = lastOperation;
        }

        public T Data { get; }
        public string Discriminator { get; }
        public byte[] RowVersion { get; }
        public Operation LastOperation { get; }

        public ulong RowVersionUInt64 => BigEndianToUInt64(RowVersion);

        static ulong BigEndianToUInt64(byte[] bigEndianBinary)
        {
            ulong result = 0;
            for (var i = 0; i < 8; i++)
            {
                result <<= 8;
                result |= bigEndianBinary[i];
            }

            return result;
        }
    }
}