using System;
using HybridDb.Config;
using ShinySwitch;

namespace HybridDb.Queue
{
    public static class CommandQueueEx
    {
        public static void UseMessageQueue(this Configuration config, string tablename = "messages")
        {
            config.GetOrAddTable(new QueueTable(tablename));

            config.Decorate<Func<DocumentTransaction, DmlCommand, Func<object>>>((container, decoratee) => (tx, command) => () =>
                Switch<object>.On(command)
                    .Match<DequeueCommand>(enqueueCommand => DequeueCommand.Execute(config.Serializer.Deserialize, tx, enqueueCommand))
                    .Else(() => decoratee(tx, command)()));
        }
    }
}