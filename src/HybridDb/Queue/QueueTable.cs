using System;
using System.Data;
using System.Linq;
using HybridDb.Config;
using HybridDb.Migrations.Schema;

namespace HybridDb.Queue
{
    public class QueueTable : BuiltInTable<QueueTable>
    {
        static QueueTable()
        {
            VersionColumn       = AddBuiltIn(new Column<string>("Version", length: 40, nullable: false, defaultValue: "0.0"));
            TopicColumn         = AddBuiltIn(new Column<string>("Topic", length: 850, nullable: false));
            PositionColumn      = AddBuiltIn(new Column<long>("Position", SqlDbType.BigInt, nullable: false));
            DiscriminatorColumn = AddBuiltIn(new Column<string>("Discriminator", length: 850, nullable: false));
            IdColumn            = AddBuiltIn(new Column<string>("Id", length: 850, nullable: false));
            OrderColumn         = AddBuiltIn(new Column<int>("Order", SqlDbType.Int, nullable: false));
            CommitIdColumn      = AddBuiltIn(new Column<Guid>("CommitId"));
            MessageColumn       = AddBuiltIn(new Column<string>("Message", length: -1));
            MetadataColumn      = AddBuiltIn(new Column<string>("Metadata", length: -1, nullable: false, defaultValue: "{}"));
            CorrelationIdColumn = AddBuiltIn(new Column<string>("CorrelationId", length: 850, nullable: false, defaultValue: "N/A"));
        }

        public QueueTable(string name) : base(name, Enumerable.Empty<Column>()) { }

        public static Column<string> VersionColumn { get; }
        public static Column<string> TopicColumn { get; }
        public static Column<long> PositionColumn { get; }
        public static Column<string> DiscriminatorColumn { get; }
        public static Column<string> IdColumn { get; }
        public static Column<int> OrderColumn { get; }
        public static Column<Guid> CommitIdColumn { get; }
        public static Column<string> MessageColumn { get; }
        public static Column<string> MetadataColumn { get; }
        public static Column<string> CorrelationIdColumn { get; }

        public override DdlCommand GetCreateCommand() => new CreateQueueTable(this);
    }
}