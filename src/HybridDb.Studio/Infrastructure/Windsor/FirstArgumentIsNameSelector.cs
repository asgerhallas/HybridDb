using System.Reflection;
using Castle.Facilities.TypedFactory;

namespace HybridDb.Studio.Infrastructure.Windsor
{
    public class FirstArgumentIsNameSelector : DefaultTypedFactoryComponentSelector
    {
        protected override string GetComponentName(MethodInfo method, object[] arguments)
        {
            return (string) arguments[0];
        }
    }
}