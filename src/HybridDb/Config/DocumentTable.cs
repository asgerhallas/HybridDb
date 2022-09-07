using System;
using System.Collections.Generic;
using System.Data;

namespace HybridDb.Config
{
    public class DocumentTable : Table
    {
        static readonly List<Column> commonColumns = new List<Column>();

        static DocumentTable()
        {
            IdColumn = AddFixed(new Column<string>("Id", length: 850, isPrimaryKey: true));
            EtagColumn = AddFixed(new Column<Guid>("Etag"));
            CreatedAtColumn = AddFixed(new Column<DateTimeOffset>("CreatedAt"));
            ModifiedAtColumn = AddFixed(new Column<DateTimeOffset>("ModifiedAt"));
            DocumentColumn = AddFixed(new Column<string>("Document", length: -1));
            MetadataColumn = AddFixed(new Column<string>("Metadata"));
            DiscriminatorColumn = AddFixed(new Column<string>("Discriminator", length: 850));
            AwaitsMigrationColumn = AddFixed(new Column<bool>("AwaitsMigration"));
            VersionColumn = AddFixed(new Column<int>("Version"));
            TimestampColumn = AddFixed(new Column<int>("Timestamp", SqlDbType.Timestamp));
            LastOperationColumn = AddFixed(new Column<byte>("LastOperation", SqlDbType.TinyInt));
        }

        public DocumentTable(string name) : base(name, commonColumns) {}

        public static Column<string> IdColumn { get; }
        public static Column<Guid> EtagColumn { get; }
        public static Column<DateTimeOffset> CreatedAtColumn { get; }
        public static Column<DateTimeOffset> ModifiedAtColumn { get; }
        public static Column<string> DocumentColumn { get; }
        public static Column<string> MetadataColumn { get; }
        public static Column<string> DiscriminatorColumn { get; }
        public static Column<bool> AwaitsMigrationColumn { get; }
        public static Column<int> VersionColumn { get; }
        public static Column<int> TimestampColumn { get; }
        public static Column<byte> LastOperationColumn { get; }

        static Column<T> AddFixed<T>(Column<T> column)
        {
            commonColumns.Add(column);
            return column;
        }
    }
}