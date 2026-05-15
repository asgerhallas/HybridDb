using HybridDb.Migrations.Schema;

namespace HybridDb.LiveQuery
{
    public class CreateChangeLogTable : DdlCommand
    {
        public CreateChangeLogTable(ChangeLogTable table)
        {
            Safe = true;
            Table = table;
        }

        public ChangeLogTable Table { get; }

        public override void Execute(DocumentStore store)
        {
            var tableName = store.Database.FormatTableName(Table.Name);

            store.Database.RawExecute($@"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = '{tableName}')
                BEGIN
                    CREATE TABLE [dbo].[{tableName}] (
                        [Id] [bigint] NOT NULL IDENTITY(0,1),
                        [Position] [rowversion] NOT NULL,
                        [CreatedAt] [datetimeoffset](7) NOT NULL,
                        [TableName] [nvarchar](850) NOT NULL,
                        [DocumentId] [nvarchar](850) NOT NULL,
                        [CommitId] [uniqueidentifier] NOT NULL,
                        [Operation] [tinyint] NOT NULL,

                        CONSTRAINT [PK_{tableName}] PRIMARY KEY CLUSTERED ([Id] ASC)
                    )

                    CREATE UNIQUE NONCLUSTERED INDEX [{tableName}_Position] ON [dbo].[{tableName}] ([Position] ASC)
                    CREATE NONCLUSTERED INDEX [{tableName}_TableName_Position] ON [dbo].[{tableName}] ([TableName] ASC, [Position] ASC)
                    CREATE NONCLUSTERED INDEX [{tableName}_CreatedAt] ON [dbo].[{tableName}] ([CreatedAt] ASC)
                END", schema: true);
        }

        public override string ToString() => $"Create live query change log table {Table.Name}";
    }
}
