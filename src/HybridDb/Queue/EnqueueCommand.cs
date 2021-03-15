using System;
using System.Data.SqlClient;
using Dapper;

namespace HybridDb.Queue
{
    public class EnqueueCommand : Command<string>
    {
        public EnqueueCommand(QueueTable table, HybridDbMessage message)
        {
            Table = table;
            Message = message;
        }

        public QueueTable Table { get; }
        public HybridDbMessage Message { get; }

        public static string Execute(Func<object, string> serializer, DocumentTransaction tx, EnqueueCommand command)
        {
            var tablename = tx.Store.Database.FormatTableNameAndEscape(command.Table.Name);
            
            var discriminator = tx.Store.Configuration.TypeMapper.ToDiscriminator(command.Message.GetType());

            try
            {
                tx.SqlConnection.Execute(@$"
                    set nocount on; 
                    insert into {tablename} (Id, CommitId, Discriminator, Message, IsFailed) 
                    values (@Id, @CommitId, @Discriminator, @Message, @IsFailed); 
                    set nocount off;",
                    new
                    {
                        command.Message.Id,
                        tx.CommitId,
                        Discriminator = discriminator,
                        Message = serializer(command.Message),
                        IsFailed = false
                    },
                    tx.SqlTransaction);
            }
            catch (SqlException e)
            {
                // primary key violations are ignored, rest is rethrown
                if (e.Number == 2627) return command.Message.Id;

                throw;
            }

            return command.Message.Id;
        }
    }
}