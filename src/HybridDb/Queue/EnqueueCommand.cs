using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using HybridDb.Config;
using HybridDb.SqlBuilder;
using Microsoft.Data.SqlClient;

namespace HybridDb.Queue
{
    public class EnqueueCommand : HybridDbCommand<string>
    {
        public const string DefaultTopic = "default";

        public EnqueueCommand(QueueTable table, HybridDbMessage message)
        {
            if (message.Id == null)
            {
                throw new ArgumentException("Message id must be set.");
            }

            if (message.Topic != null && string.IsNullOrWhiteSpace(message.Topic))
            {
                throw new ArgumentException("Message topic can not be empty or whitespace only.");
            }

            Table = table;
            Message = message with { Topic = message.Topic ?? DefaultTopic };
        }

        public QueueTable Table { get; }
        public HybridDbMessage Message { get; }

        public static string Execute(Func<object, string> serializer, DocumentTransaction tx, EnqueueCommand command)
        {
            var options = tx.Store.Configuration.Resolve<MessageQueueOptions>();

            command.Message.Metadata[HybridDbMessage.EnqueuedAtKey] = DateTimeOffset.Now.ToString("O");

            var projections = new Dictionary<Column, object>
            {
                [QueueTable.TopicColumn] = command.Message.Topic,
                [QueueTable.VersionColumn] = options.Version.ToString(),
                [QueueTable.IdColumn] = command.Message.Id,
                [QueueTable.OrderColumn] = command.Message.Order,
                [QueueTable.CommitIdColumn] = tx.CommitId,
                [QueueTable.DiscriminatorColumn] = tx.Store.Configuration.TypeMapper.ToDiscriminator(command.Message.Payload.GetType()),
                [QueueTable.MessageColumn] = serializer(command.Message.Payload),
                [QueueTable.MetadataColumn] = serializer(command.Message.Metadata),
                [QueueTable.CorrelationIdColumn] = command.Message.CorrelationId,
            };

            var columns = Sql.Join(", ", projections.Keys.Select(col => Sql.From($"{col}")));
            var values = Sql.Join(", ", projections.Select(x => Sql.Empty.Append(x.Value, x.Key)));

            var sql = Sql.From($"set nocount on; insert into {command.Table} ({columns}) values ({values}); set nocount off;")
                .Build(tx.Store, out var parameters);

            try
            {
                tx.SqlConnection.Execute(sql, parameters, tx.SqlTransaction);
            }
            catch (SqlException e)
            {
                // Enqueuing is idempotent. It should ignore exceptions from primary key or unique index violations.
                if (e.Number is 2627 or 2601) return command.Message.Id;

                throw;
            }

            return command.Message.Id;
        }
    }
}