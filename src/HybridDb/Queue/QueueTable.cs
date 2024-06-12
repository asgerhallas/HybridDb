using System;
using System.Data;
using HybridDb.Config;
using HybridDb.Migrations.Schema;

namespace HybridDb.Queue
{
    public class QueueTable : Table
    {
        public QueueTable(string name) : base(name,
            new Column<string>("Version", length: 40, nullable: false, defaultValue: "0.0"),
            new Column<string>("Topic", length: 850, nullable: false),
            new Column<long>("Position", SqlDbType.BigInt, nullable: false),
            new Column<string>("Discriminator", length: 850, nullable: false),
            new Column<string>("Id", length: 850, nullable: false),
            new Column<int>("Order", SqlDbType.Int, nullable: false),
            new Column<Guid>("CommitId"),
            new Column<string>("Message", length: -1),
            new Column<string>("Metadata", length: -1, nullable: false, defaultValue: "{}"),
            new Column<string>("CorrelationId", length: 850, nullable: false, defaultValue: "UNKNOWN")) { }

        public override DdlCommand GetCreateCommand() => new CreateQueueTable(this);
    }
}