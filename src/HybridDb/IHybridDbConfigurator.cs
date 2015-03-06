using System;

namespace HybridDb
{
    public interface IHybridDbConfigurator
    {
        Configuration.Configuration Configure();
    }

    public class NullHybridDbConfigurator : IHybridDbConfigurator
    {
        public Configuration.Configuration Configure()
        {
            return new Configuration.Configuration();
        }
    }

    public class LambdaHybridDbConfigurator : HybridDbConfigurator
    {
        public LambdaHybridDbConfigurator(Action<Configuration.Configuration> configurator)
        {
            configurator(configuration);
        }
    }
}