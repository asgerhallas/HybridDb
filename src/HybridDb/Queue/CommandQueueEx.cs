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

            config.Decorate<Func<DocumentTransaction, DmlCommand, Func<object>>>((_, decoratee) => (tx, command) => () =>
                Switch<object>.On(command)
                    .Match<EnqueueCommand>(enqueueCommand => EnqueueCommand.Execute(config.Serializer.Serialize, tx, enqueueCommand))
                    .Match<DequeueCommand>(dequeueCommand => DequeueCommand.Execute(config.Serializer.Deserialize, tx, dequeueCommand))
                    .Else(() => decoratee(tx, command)()));
        }
    }
}