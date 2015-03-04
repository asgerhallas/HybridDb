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

        protected DocumentDesigner<TEntity> Document<TEntity>(string tablename = null)
        {
            return config.Document<TEntity>(tablename);
        }

        protected IndexDesigner<TIndex, TEntity> Document<TEntity, TIndex>()
        {
            return config.Index<TIndex, TEntity>();
        }
    }
}