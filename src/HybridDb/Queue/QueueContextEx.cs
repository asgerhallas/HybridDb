using System;
using System.Collections.Generic;

namespace HybridDb.Queue
{
    public static class QueueContextEx
    {
        public static SessionContext GetSessionContextOrDefault(this IDocumentSession session) =>
            (SessionContext)session.Advanced.SessionData.GetValueOrDefault(SessionContext.Key);

        public static SessionContext GetSessionContext(this IDocumentSession session) =>
            (SessionContext)session.Advanced.SessionData.GetValueOrDefault(SessionContext.Key)
            ?? throw new InvalidOperationException("""
                                                   There's no SessionContext in this session. 
                                                   Are you sure it's a session provided by the message queue?
                                                   """);

        public static MessageContext GetMessageContextOrDefault(this IDocumentSession session) =>
            (MessageContext)session.Advanced.SessionData.GetValueOrDefault(MessageContext.Key);

        public static MessageContext GetMessageContext(this IDocumentSession session) =>
            (MessageContext)session.Advanced.SessionData.GetValueOrDefault(MessageContext.Key)
            ?? throw new InvalidOperationException("""
                                                   There's no MessageContext in this session. 
                                                   Are you sure it's a session provided by the message queue and you are in a message handler?
                                                   """);
    }
}