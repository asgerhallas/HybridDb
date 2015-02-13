using System;

namespace HybridDb
{
    public interface IHybridDbConfigurator
    {
        void Configure(Configuration configuration);
    }

    public class LambdaHybridDbConfigurator : IHybridDbConfigurator 
    {
        readonly Action<Configuration> configurator;

        public LambdaHybridDbConfigurator(Action<Configuration> configurator)
        {
            this.configurator = configurator;
        }

        public void Configure(Configuration configuration)
        {
            configurator(configuration);
        }
    }
}