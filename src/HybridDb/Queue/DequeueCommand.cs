using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;

namespace HybridDb.Queue
{
    public class DequeueCommand : HybridDbCommand<HybridDbMessage>
    {
        public DequeueCommand(QueueTable table, IReadOnlyList<string> topics)
        {
            Table = table ?? throw new ArgumentNullException(nameof(table));

            if (topics == null) throw new ArgumentNullException(nameof(topics));
            if (topics.Count == 0) throw new ArgumentException("Value cannot be an empty collection.", nameof(topics));

            Topics = topics;
        }

        public DequeueCommand(QueueTable table, string messageId)
        {
            Table = table ?? throw new ArgumentNullException(nameof(table));

            MessageId = messageId ?? throw new ArgumentNullException(nameof(messageId));
        }

        public QueueTable Table { get; }
        public IReadOnlyList<string> Topics { get; }
        public string MessageId { get; }

        public static HybridDbMessage Execute(Func<string, Type, object> deserializer, DocumentTransaction tx, DequeueCommand command)
        {
            var options = tx.Store.Configuration.Resolve<MessageQueueOptions>();
            var tablename = tx.Store.Database.FormatTableNameAndEscape(command.Table.Name);

            var (where, param) = (command.Topics, command.MessageId) switch
            {
                (not null, _) => ("where Topic in @Topics", (object)new
                {
                    command.Topics,
                    Version = options.Version.ToString()
                }),
                (_, not null) => ("where Id = @MessageId", new
                {
                    command.MessageId,
                    Version = options.Version.ToString()
                }),
                _ => throw new ArgumentException()
            };

            var msg = tx.SqlConnection
                .Query<(string Id, string Payload, string Discriminator, string Topic, int Order, string Metadata, string CorrelationId)>($@"
                    set nocount on;
                    with x as (
                        select top(1) * from {tablename} with (rowlock, readpast) 
                        {where}
                        and cast('/' + Version + '/' as hierarchyid) <= cast('/' + @Version + '/' as hierarchyid)
                        order by [Order] asc, Position asc
                    )
                    delete from x output deleted.Id, deleted.Message as Payload, deleted.Discriminator, deleted.Topic, deleted.[Order], deleted.Metadata, deleted.CorrelationId;
                    set nocount off;",
                    param,
                    tx.SqlTransaction
                ).SingleOrDefault();

            if (msg == default) return null;

            var type = tx.Store.Configuration.TypeMapper.ToType(typeof(object), msg.Discriminator);

            var metadata = (Dictionary<string, string>)deserializer(msg.Metadata, typeof(Dictionary<string, string>));

            return new HybridDbMessage(msg.Id, deserializer(msg.Payload, type), msg.Topic, msg.Order, msg.CorrelationId)
            {
                Metadata = metadata
            };
        }
    }
}