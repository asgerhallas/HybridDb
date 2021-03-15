using System;
using HybridDb.Config;

namespace HybridDb.Queue
{
    public class QueueTable : Table
    {
        public QueueTable(string name) : base(name,
            new Column<string>("Id", length: 850, isPrimaryKey: true),
            new Column<Guid>("CommitId"),
            new Column<string>("Discriminator", length: 850),
            new Column<string>("Message", length: -1),
            new Column<bool>("IsFailed"))
        { }
    }
}