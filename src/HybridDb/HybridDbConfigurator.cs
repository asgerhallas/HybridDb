using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;
using HybridDb.Migrations;
using HybridDb.Serialization;
using Serilog;

namespace HybridDb
{
    public abstract class HybridDbConfigurator : IHybridDbConfigurator
    {
        internal Configuration configuration;

        protected HybridDbConfigurator()
        {
            configuration = new Configuration();
        }
        
        public Configuration Configure()
        {
            return configuration;
        }

        protected virtual void Reset()
        {
            configuration = new Configuration();
        }

        protected DocumentDesigner<TEntity> Document<TEntity>(string tablename = null, string discriminator = null)
        {
            return configuration.Document<TEntity>(tablename, discriminator);
        }

        protected void UseSerializer(ISerializer serializer)
        {
            configuration.UseSerializer(serializer);
        }

        protected IDefaultSerializerConfigurator UseDefaultSerializer()
        {
            var serializer = new DefaultSerializer();
            configuration.UseSerializer(serializer);
            return serializer;
        }

        protected void UseLogger(ILogger logger)
        {
            configuration.UseLogger(logger);
        }

        protected void UseMigrations(IReadOnlyList<Migration> migrations)
        {
            configuration.UseMigrations(migrations);
        }

        protected void UseMigrations(params Migration[] migrations)
        {
            configuration.UseMigrations(migrations.ToList());
        }

        protected void UseBackupWriter(IBackupWriter writer)
        {
            configuration.UseBackupWriter(writer);
        }

        internal void DisableMigrations()
        {
            configuration.DisableMigrationsOnStartup();
        }

        protected void DisableDocumentMigrationsInBackground()
        {
            configuration.DisableDocumentMigrationsOnStartup();
        }
    }
}