using HybridDb.Migrations.Schema;

namespace HybridDb.Events.Commands
{
    public class CreateEventTable : SchemaMigrationCommand
    {
        public EventTable EventTable { get; }

        public CreateEventTable(EventTable eventTable) => EventTable = eventTable;

        public override string ToString() => "Create event table";

        public static void CreateEventTableExecutor(DocumentStore store, CreateEventTable command)
        {
            // ALTER DATABASE ""{store.Database}"" SET ALLOW_SNAPSHOT_ISOLATION ON

            var tableName = store.Database.FormatTableName(command.EventTable.Name);

            store.Database.RawExecute($@"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = '{tableName}')
                BEGIN                       
                    CREATE TABLE [dbo].[{tableName}] (
	                    [globSeq] [bigint] NOT NULL IDENTITY(0,1),
                        [id] [uniqueidentifier] NOT NULL,
	                    [batch] [uniqueidentifier] NOT NULL,
	                    [stream] [nvarchar](850) NOT NULL,
                        [name] [nvarchar](850) NOT NULL,
	                    [seq] [bigint] NOT NULL,
                        [gen] [nvarchar](10) NOT NULL,
	                    [meta] [nvarchar](max) NULL,
	                    [data] [varbinary](max) NULL,

                        CONSTRAINT [PK_{tableName}] PRIMARY KEY CLUSTERED 
                        (
	                        [globSeq] ASC
                        ) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
                    )

                    CREATE UNIQUE NONCLUSTERED INDEX [IDX_{tableName}_stream_seq] ON [dbo].[{tableName}]
                    (
	                    [stream] ASC,
	                    [seq] ASC
                    ) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)

                    CREATE UNIQUE NONCLUSTERED INDEX [IDX_{tableName}_event_id] ON [dbo].[{tableName}]
                    (
                        [id]
                    )  

                    CREATE NONCLUSTERED INDEX [IDX_{tableName}_commit_id] ON [dbo].[{tableName}]
                    (
                        [batch]
                    )  

                END", schema: true);
        }
    }
}