using System;
using System.Data;

namespace HybridDb.Config
{
    public class DocumentTable : Table
    {
        public DocumentTable(string name) : base(name)
        {
            IdColumn = Add(new Column("Id", typeof(string), length: 850, isPrimaryKey: true));
            EtagColumn = Add(new Column("Etag", typeof(Guid)));
            CreatedAtColumn = Add(new Column("CreatedAt", typeof(DateTimeOffset)));
            ModifiedAtColumn = Add(new Column("ModifiedAt", typeof(DateTimeOffset)));
            DocumentColumn = Add(new Column("Document", typeof(string)));
            MetadataColumn = Add(new Column("Metadata", typeof(string)));
            DiscriminatorColumn = Add(new Column("Discriminator", typeof(string), length: 850));
            AwaitsReprojectionColumn = Add(new Column("AwaitsReprojection", typeof(bool)));
            VersionColumn = Add(new Column("Version", typeof(int)));
            TimestampColumn = Add(new Column("Timestamp", SqlDbType.Timestamp, typeof(int)));
            LastOperationColumn = Add(new Column("LastOperation", SqlDbType.TinyInt, typeof(byte)));
        }

        public Column IdColumn { get; }
        public Column EtagColumn { get; }
        public Column CreatedAtColumn { get; }
        public Column ModifiedAtColumn { get; }
        public Column DocumentColumn { get; }
        public Column MetadataColumn { get; }
        public Column DiscriminatorColumn { get; }
        public Column AwaitsReprojectionColumn { get; }
        public Column VersionColumn { get; }
        public Column TimestampColumn { get; }
        public Column LastOperationColumn { get; }
    }
}