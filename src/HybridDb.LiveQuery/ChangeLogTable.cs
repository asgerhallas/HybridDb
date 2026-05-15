using System;
using System.Data;
using HybridDb.Config;
using HybridDb.Migrations.Schema;

namespace HybridDb.LiveQuery
{
    public class ChangeLogTable : Table
    {
        public ChangeLogTable(string name) : base(name,
            new Column<long>("Id", nullable: false, isPrimaryKey: true),
            new Column<byte[]>("Position", SqlDbType.Timestamp),
            new Column<DateTimeOffset>("CreatedAt", nullable: false),
            new Column<string>("TableName", length: 850, nullable: false),
            new Column<string>("DocumentId", length: 850, nullable: false),
            new Column<Guid>("CommitId", nullable: false),
            new Column<byte>("Operation", SqlDbType.TinyInt, nullable: false))
        {
        }

        public override DdlCommand GetCreateCommand() => new CreateChangeLogTable(this);
    }
}
