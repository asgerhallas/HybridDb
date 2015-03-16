using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;
using HybridDb.Logging;
using HybridDb.Migrations;

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

        protected DocumentDesigner<TEntity> Document<TEntity>(string tablename = null, string discriminator = null)
        {
            return configuration.Document<TEntity>(tablename, discriminator);
        }

        protected void UseSerializer(ISerializer serializer)
        {
            configuration.UseSerializer(serializer);
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
    }
}