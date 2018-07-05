using System;

namespace HybridDb
{
    public static class DocumentSessionEx
    {
        public static object Load(this IDocumentSession session, Type type, string key) => session.LoadAsync(type, key).Result;
        public static T Load<T>(this IDocumentSession session, string key) where T : class => session.LoadAsync<T>(key).Result;
        public static void SaveChanges(this IDocumentSession session) => session.SaveChangesAsync().Wait();

        public static void SaveChanges(this IDocumentSession session, bool lastWriteWins, bool forceWriteUnchangedDocument) =>
            session.SaveChangesAsync(lastWriteWins, forceWriteUnchangedDocument).Wait();
    }
}