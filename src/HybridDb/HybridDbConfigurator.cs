using HybridDb.Schema;

namespace HybridDb
{
    public abstract class HybridDbConfigurator : IHybridDbConfigurator
    {
        protected readonly Configuration configuration;

        protected HybridDbConfigurator()
        {
            configuration = new Configuration();
        }
        
        public Configuration Configure()
        {
            return configuration;
        }

        protected DocumentDesigner<TEntity> Document<TEntity>(string tablename = null)
        {
            return configuration.Document<TEntity>(tablename);
        }

        protected void UseSerializer(ISerializer serializer)
        {
            configuration.UseSerializer(serializer);
        }
    }
}