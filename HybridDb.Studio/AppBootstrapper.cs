using System;
using Caliburn.Micro;
using Castle.Windsor;
using HybridDb.Studio.ViewModels;
using System.Linq;

namespace HybridDb.Studio
{
    public class AppBootstrapper : Bootstrapper<ShellViewModel>
    {
        WindsorContainer container;

        protected override void Configure()
        {
            container = new WindsorContainer();
        }

        protected override object GetInstance(Type service, string key)
        {
            return container.Resolve(key, service);
        }

        protected override System.Collections.Generic.IEnumerable<object> GetAllInstances(Type service)
        {
            return container.ResolveAll(service).OfType<object>();
        }
    }
}