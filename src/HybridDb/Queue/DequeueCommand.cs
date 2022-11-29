using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;

namespace HybridDb.Queue
{
    public class DequeueCommand : Command<HybridDbMessage>
    {
        public DequeueCommand(QueueTable table, IReadOnlyList<string> topics)
        {
            Table = table ?? throw new ArgumentNullException(nameof(table));

            if (topics == null) throw new ArgumentNullException(nameof(topics));
            if (topics.Count == 0) throw new ArgumentException("Value cannot be an empty collection.", nameof(topics));

            Topics = topics;
        }

        public QueueTable Table { get; }
        public IReadOnlyList<string> Topics { get; }

        public static HybridDbMessage Execute(Func<string, Type, object> deserializer, DocumentTransaction tx,
            DequeueCommand command)
        {
            var options = tx.Store.Configuration.Resolve<MessageQueueOptions>();
            var tablename = tx.Store.Database.FormatTableNameAndEscape(command.Table.Name);

            var msg = tx.SqlConnection
                .Query<(string Id, string Payload, string Discriminator, string Topic, string Metadata)>($@"
                    set nocount on;
                    with x as (
                        select top(1) * from {tablename} with (rowlock, readpast) 
                        where Topic in @Topics
                        and cast('/' + Version + '/' as hierarchyid) <= cast('/' + @Version + '/' as hierarchyid)
                        order by [Order] asc, Position asc
                    )
                    delete from x output deleted.Id, deleted.Message as Payload, deleted.Discriminator, deleted.Topic, deleted.Metadata;
                    set nocount off;",
                    new
                    {
                        command.Topics,
                        Version = options.Version.ToString()
                    },
                    tx.SqlTransaction
                ).SingleOrDefault();

            if (msg == default) return null;

            var type = tx.Store.Configuration.TypeMapper.ToType(typeof(object), msg.Discriminator);

            var metadata = (Dictionary<string, string>)deserializer(msg.Metadata, typeof(Dictionary<string, string>));

            return new HybridDbMessage(msg.Id, deserializer(msg.Payload, type), msg.Topic)
            {
                Metadata = metadata
            };
        }
    }
}