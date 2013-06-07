using System;
using System.Data;

namespace HybridDb.Schema
{
    public class DocumentTable : Table
    {
        public DocumentTable(string name) : base(name)
        {
            DocumentColumn = new Column("Document", new SqlColumn(DbType.Binary, Int32.MaxValue, nullable: true));
            Register(DocumentColumn);

            VersionColumn = new Column("Version", new SqlColumn(DbType.Int32, nullable: true));
            Register(VersionColumn);

            SizeColumn = new Column("Size", new SqlColumn(DbType.Int32, defaultValue: 0));
            Register(SizeColumn);
        }

        public Column DocumentColumn { get; private set; }
        public Column VersionColumn { get; private set; }
        public Column SizeColumn { get; private set; }
    }
}