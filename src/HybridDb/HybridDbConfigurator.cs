using HybridDb.Logging;
using HybridDb.Schema;

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
    }
}