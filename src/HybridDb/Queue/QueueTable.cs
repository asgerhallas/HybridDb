using System;
using HybridDb.Config;
using HybridDb.Migrations.Schema;

namespace HybridDb.Queue
{
    public class QueueTable : Table
    {
        public QueueTable(string name) : base(name,
            new Column<string>("Topic", length: 850, nullable: false),
            new Column<string>("Version", length: 40, nullable: false, defaultValue: "0.0"),
            new Column<string>("Id", length: 850, nullable: false),
            new Column<Guid>("CommitId"),
            new Column<string>("Discriminator", length: 850, nullable: false),
            new Column<string>("Message", length: -1))
        { }

        public override DdlCommand GetCreateCommand() => new CreateQueueTable(this);
    }
}