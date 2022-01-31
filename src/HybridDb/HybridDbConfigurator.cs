using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;
using HybridDb.Migrations;
using HybridDb.Migrations.Documents;
using HybridDb.Queue;
using HybridDb.Serialization;
using Microsoft.Extensions.Logging;

namespace HybridDb
{
    public abstract class HybridDbConfigurator
    {
        internal Configuration configuration;

        protected HybridDbConfigurator() => configuration = new Configuration();

        protected DocumentDesigner<TEntity> Document<TEntity>(string tablename = null, string discriminator = null) => configuration.Document<TEntity>(tablename, discriminator);

        protected void UseSerializer(ISerializer serializer) => configuration.UseSerializer(serializer);

        protected void UseTypeMapper(ITypeMapper typeMapper) => configuration.UseTypeMapper(typeMapper);

        protected IDefaultSerializerConfigurator UseDefaultSerializer()
        {
            var serializer = new DefaultSerializer();
            configuration.UseSerializer(serializer);
            return serializer;
        }

        protected void UseLogger(ILogger logger) => configuration.UseLogger(logger);

        protected void UseMigrations(IReadOnlyList<Migration> migrations) => configuration.UseMigrations(migrations);

        protected void UseMigrations(params Migration[] migrations) => configuration.UseMigrations(migrations.ToList());
        
        protected void UseMessageQueue(string tablename = "messages") => configuration.UseMessageQueue(tablename);

        protected void UseBackupWriter(IBackupWriter writer) => configuration.UseBackupWriter(writer);

        protected void UseTableNamePrefix(string prefix) => configuration.UseTableNamePrefix(prefix);

        protected void UseKeyResolver(Func<object, string> resolver) => configuration.UseKeyResolver(resolver);

        protected void UseSoftDelete() => configuration.UseSoftDelete();

        protected void UseEventStore() => configuration.UseEventStore();

        internal void DisableMigrations() => configuration.DisableMigrations();

        protected void DisableBackgroundMigrations() => configuration.DisableBackgroundMigrations();
    }
}