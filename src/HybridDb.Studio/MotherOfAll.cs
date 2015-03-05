using System;
using System.Linq;
using System.Reflection;

namespace HybridDb.Studio
{
    public class MotherOfAll
    {
        public IHybridDbConfigurator GetConfiguratorFromAssembly(string assemblyPath)
        {
            try
            {
                var configuration = Assembly.LoadFrom(assemblyPath)
                    .GetTypes()
                    .SingleOrDefault(x => typeof (IHybridDbConfigurator).IsAssignableFrom(x));

                return (IHybridDbConfigurator)Activator.CreateInstance(configuration);
            }
            catch (Exception e)
            {
                return null;
            }
        }
    }
}