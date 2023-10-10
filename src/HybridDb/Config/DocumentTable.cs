using System;
using System.Data;
using System.Linq;
using HybridDb.Migrations.Schema;

namespace HybridDb.Config
{
    public class DocumentTable : BuiltInTable<DocumentTable>
    {
        static DocumentTable()
        {
            IdColumn = AddBuiltIn(new Column<string>("Id", length: 850, isPrimaryKey: true));
            EtagColumn = AddBuiltIn(new Column<Guid>("Etag"));
            CreatedAtColumn = AddBuiltIn(new Column<DateTimeOffset>("CreatedAt"));
            ModifiedAtColumn = AddBuiltIn(new Column<DateTimeOffset>("ModifiedAt"));
            DocumentColumn = AddBuiltIn(new Column<string>("Document", length: -1));
            MetadataColumn = AddBuiltIn(new Column<string>("Metadata"));
            DiscriminatorColumn = AddBuiltIn(new Column<string>("Discriminator", length: 850));
            AwaitsReprojectionColumn = AddBuiltIn(new Column<bool>("AwaitsReprojection"));
            VersionColumn = AddBuiltIn(new Column<int>("Version"));
            TimestampColumn = AddBuiltIn(new Column<int>("Timestamp", SqlDbType.Timestamp));
            LastOperationColumn = AddBuiltIn(new Column<byte>("LastOperation", SqlDbType.TinyInt));
        }

        public DocumentTable(string name) : base(name, Enumerable.Empty<Column>()) {}

        public override DdlCommand GetCreateCommand() => base.GetCreateCommand();

        public static Column<string> IdColumn { get; }
        public static Column<Guid> EtagColumn { get; }
        public static Column<DateTimeOffset> CreatedAtColumn { get; }
        public static Column<DateTimeOffset> ModifiedAtColumn { get; }
        public static Column<string> DocumentColumn { get; }
        public static Column<string> MetadataColumn { get; }
        public static Column<string> DiscriminatorColumn { get; }
        public static Column<bool> AwaitsReprojectionColumn { get; }
        public static Column<int> VersionColumn { get; }
        public static Column<int> TimestampColumn { get; }
        public static Column<byte> LastOperationColumn { get; }
    }
}