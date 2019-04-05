using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;
using HybridDb.Migrations;
using HybridDb.Serialization;
using Serilog;

namespace HybridDb
{
    public abstract class HybridDbConfigurator
    {
        internal Configuration configuration;

        protected HybridDbConfigurator() => configuration = new Configuration();

        protected DocumentDesigner<TEntity> Document<TEntity>(string tablename = null) => configuration.Document<TEntity>(tablename);

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

        protected void UseBackupWriter(IBackupWriter writer) => configuration.UseBackupWriter(writer);

        protected void UseTableNamePrefix(string prefix) => configuration.UseTableNamePrefix(prefix);

        protected void UseKeyResolver(Func<object, string> resolver) => configuration.UseKeyResolver(resolver);

        protected void UseQueues() => configuration.UseQueues();

        protected void UseEventStore() => configuration.UseEventStore();

        internal void DisableMigrations() => configuration.DisableMigrationsOnStartup();

        protected void DisableDocumentMigrationsInBackground() => configuration.DisableDocumentMigrationsOnStartup();
    }
}