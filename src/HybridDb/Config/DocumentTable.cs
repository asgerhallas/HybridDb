using System;
using System.Data;

namespace HybridDb.Config
{
    public class DocumentTable : Table
    {
        public DocumentTable(string name) : base(name)
        {
            IdColumn = new SystemColumn("Id", typeof(Guid), new SqlColumn(DbType.Guid, isPrimaryKey: true));
            Register(IdColumn);

            EtagColumn = new SystemColumn("Etag", typeof(Guid), new SqlColumn(DbType.Guid));
            Register(EtagColumn);

            CreatedAtColumn = new SystemColumn("CreatedAt", typeof(DateTimeOffset), new SqlColumn(DbType.DateTimeOffset));
            Register(CreatedAtColumn);

            ModifiedAtColumn = new SystemColumn("ModifiedAt", typeof(DateTimeOffset), new SqlColumn(DbType.DateTimeOffset));
            Register(ModifiedAtColumn);

            DocumentColumn = new Column("Document", typeof(byte[]), new SqlColumn(DbType.Binary, Int32.MaxValue, nullable: true));
            Register(DocumentColumn);

            DiscriminatorColumn = new Column("Discriminator", typeof(string), new SqlColumn(DbType.StringFixedLength, 255, nullable: true));
            Register(DiscriminatorColumn);

            VersionColumn = new Column("Version", typeof(int), new SqlColumn(DbType.Int32, nullable: true));
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