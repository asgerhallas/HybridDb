using System;

namespace HybridDb.Config
{
    public class DocumentTable : Table
    {
        public DocumentTable(string name) : base(name)
        {
            IdColumn = new SystemColumn("Id", typeof(string), length: 1024, isPrimaryKey: true);
            Register(IdColumn);

            EtagColumn = new SystemColumn("Etag", typeof(Guid));
            Register(EtagColumn);

            CreatedAtColumn = new SystemColumn("CreatedAt", typeof(DateTimeOffset));
            Register(CreatedAtColumn);

            ModifiedAtColumn = new SystemColumn("ModifiedAt", typeof(DateTimeOffset));
            Register(ModifiedAtColumn);

            DocumentColumn = new Column("Document", typeof(byte[]), int.MaxValue);
            Register(DocumentColumn);

            MetadataColumn = new Column("Metadata", typeof(byte[]), int.MaxValue);
            Register(MetadataColumn);

            DiscriminatorColumn = new Column("Discriminator", typeof(string), length: 1024);
            Register(DiscriminatorColumn);

            AwaitsReprojectionColumn = new Column("AwaitsReprojection", typeof(bool));
            Register(AwaitsReprojectionColumn);

            VersionColumn = new Column("Version", typeof(int));
            Register(VersionColumn);
        }

        public SystemColumn IdColumn { get; private set; }
        public SystemColumn EtagColumn { get; private set; }
        public SystemColumn CreatedAtColumn { get; private set; }
        public SystemColumn ModifiedAtColumn { get; private set; }
        public Column DocumentColumn { get; private set; }
        public Column MetadataColumn { get; private set; }
        public Column DiscriminatorColumn { get; private set; }
        public Column AwaitsReprojectionColumn { get; private set; }
        public Column VersionColumn { get; private set; }
    }
}