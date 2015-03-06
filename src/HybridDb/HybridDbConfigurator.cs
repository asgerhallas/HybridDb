using HybridDb.Configuration;
using HybridDb.Logging;

namespace HybridDb
{
    public abstract class HybridDbConfigurator : IHybridDbConfigurator
    {
        internal Configuration.Configuration configuration;

        protected HybridDbConfigurator()
        {
            configuration = new Configuration.Configuration();
        }
        
        public Configuration.Configuration Configure()
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
    }
}