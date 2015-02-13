using HybridDb.Schema;

namespace HybridDb
{
    public abstract class HybridDbConfigurator : IHybridDbConfigurator
    {
        Configuration config;

        protected abstract void Configure();

        public void Configure(Configuration configuration)
        {
            config = configuration;
            Configure();
        }

        protected DocumentDesign<TEntity> Document<TEntity>(string tablename = null)
        {
            return config.Document<TEntity>(tablename);
        }
    }
}