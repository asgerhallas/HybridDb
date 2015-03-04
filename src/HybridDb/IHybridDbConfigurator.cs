using System;

namespace HybridDb
{
    public interface IHybridDbConfigurator
    {
        Configuration Configure();
    }

    public class NullHybridDbConfigurator : IHybridDbConfigurator
    {
        public Configuration Configure()
        {
            return new Configuration();
        }
    }

    public class LambdaHybridDbConfigurator : HybridDbConfigurator
    {
        public LambdaHybridDbConfigurator(Action<Configuration> configurator)
        {
            configurator(configuration);
        }
    }
}