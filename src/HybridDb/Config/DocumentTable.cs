using System;
using System.Data;

namespace HybridDb.Config
{
    public class DocumentTable : Table
    {
        public DocumentTable(string name) : base(name)
        {
            IdColumn = new Column("Id", typeof(string), length: 900, isPrimaryKey: true);
            Register(IdColumn);

            EtagColumn = new Column("Etag", typeof(Guid));
            Register(EtagColumn);

            CreatedAtColumn = new Column("CreatedAt", typeof(DateTimeOffset));
            Register(CreatedAtColumn);

            ModifiedAtColumn = new Column("ModifiedAt", typeof(DateTimeOffset));
            Register(ModifiedAtColumn);

            DocumentColumn = new Column("Document", typeof(byte[]));
            Register(DocumentColumn);

            MetadataColumn = new Column("Metadata", typeof(byte[]));
            Register(MetadataColumn);

            DiscriminatorColumn = new Column("Discriminator", typeof(string), length: 900);
            Register(DiscriminatorColumn);

            AwaitsReprojectionColumn = new Column("AwaitsReprojection", typeof(bool));
            Register(AwaitsReprojectionColumn);

            VersionColumn = new Column("Version", typeof(int));
            Register(VersionColumn);

            RowVersionColumn = new Column("VirtualTime", SqlDbType.Timestamp, typeof(int));
            Register(RowVersionColumn);

            LastOperationColumn = new Column("LastOperation", SqlDbType.TinyInt, typeof(byte));
            Register(LastOperationColumn);
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
        public Column RowVersionColumn { get; }
        public Column LastOperationColumn { get; }
    }
}