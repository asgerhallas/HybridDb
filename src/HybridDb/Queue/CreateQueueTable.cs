using HybridDb.Migrations.Schema;

namespace HybridDb.Queue
{
    public class CreateQueueTable : DdlCommand
    {
        public QueueTable QueueTable { get; }

        public CreateQueueTable(QueueTable queueTable)
        {
            Safe = true;

            QueueTable = queueTable;
        }

        public override string ToString() => "Create queue table";

        public override void Execute(DocumentStore store)
        {
            var tableName = store.Database.FormatTableName(QueueTable.Name);

            store.Database.RawExecute($@"
                if (object_id('{tableName}', 'U') is null)
                begin
                    CREATE TABLE [dbo].[{tableName}] (
                        [Version] [nvarchar](40) NOT NULL default '0.0',
                        [Topic] [nvarchar](850) NOT NULL,
                        [Position] [bigint] NOT NULL IDENTITY(0,1),
	                    [Discriminator] [nvarchar](850) NOT NULL,
	                    [Id] [nvarchar](850) NOT NULL,
                        [Order] [int] NOT NULL default (1),
	                    [CommitId] [uniqueidentifier] NOT NULL,
	                    [Message] [nvarchar](max) NULL,
	                    [Metadata] [nvarchar](max) NULL default '{{}}',
                        [CorrelationId] [nvarchar](850) NOT NULL,

                        CONSTRAINT [PK_{tableName}] PRIMARY KEY CLUSTERED ([Topic] ASC, [Order] ASC, [Position] ASC)
                    )
    
                    CREATE UNIQUE NONCLUSTERED INDEX [{tableName}_Topic_Id] ON [dbo].[{tableName}] ([Topic], [Id])  
                end", schema: true);
        }
    }
}