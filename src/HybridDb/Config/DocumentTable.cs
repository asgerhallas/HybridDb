using System;
using System.Data;

namespace HybridDb.Config
{
    public class DocumentTable : Table
    {
        public DocumentTable(string name) : base(name)
        {
            IdColumn = new SystemColumn("Id", typeof(Guid),  isPrimaryKey: true);
            Register(IdColumn);

            EtagColumn = new SystemColumn("Etag", typeof(Guid));
            Register(EtagColumn);

            CreatedAtColumn = new SystemColumn("CreatedAt", typeof(DateTimeOffset));
            Register(CreatedAtColumn);

            ModifiedAtColumn = new SystemColumn("ModifiedAt", typeof(DateTimeOffset));
            Register(ModifiedAtColumn);

            DocumentColumn = new Column("Document", typeof(byte[]), length: Int32.MaxValue, nullable: true);
            Register(DocumentColumn);

            DiscriminatorColumn = new Column("Discriminator", typeof(string), length: 255, nullable: true);  //TODO: should be fixed length?!
            Register(DiscriminatorColumn);

            VersionColumn = new Column("Version", typeof(int), nullable: true);
            Register(VersionColumn);
        }

        public SystemColumn IdColumn { get; private set; }
        public SystemColumn EtagColumn { get; private set; }
        public SystemColumn CreatedAtColumn { get; private set; }
        public SystemColumn ModifiedAtColumn { get; private set; }
        public Column DocumentColumn { get; private set; }
        public Column DiscriminatorColumn { get; private set; }
        public Column VersionColumn { get; private set; }
    }
}