using System;

namespace HybridDb.LiveQuery
{
    public class DocumentChange
    {
        public DocumentChange(string tableName, string documentId, Guid commitId, Operation operation, byte[] position, DateTimeOffset createdAt)
        {
            TableName = tableName;
            DocumentId = documentId;
            CommitId = commitId;
            Operation = operation;
            Position = position;
            CreatedAt = createdAt;
        }

        public string TableName { get; }
        public string DocumentId { get; }
        public Guid CommitId { get; }
        public Operation Operation { get; }
        public byte[] Position { get; }
        public DateTimeOffset CreatedAt { get; }

        public ulong PositionUInt64 => BigEndianToUInt64(Position);

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
