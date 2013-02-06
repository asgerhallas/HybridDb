using System;
using Caliburn.Micro;
using Castle.Facilities.TypedFactory;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using HybridDb.Studio.ViewModels;
using System.Linq;
using HybridDb.Studio.Views;

namespace HybridDb.Studio
{
    public class AppBootstrapper : Bootstrapper<ShellViewModel>
    {
        WindsorContainer container;

        protected override void Configure()
        {
            container = new WindsorContainer();
            container.AddFacility<TypedFactoryFacility>();
            container.Register(Component.For<ShellViewModel>().ImplementedBy<ShellViewModel>());
            container.Register(Component.For<DocumentViewModel>().LifeStyle.Transient);

            container.Register(Component.For<ShellView>());
            container.Register(Component.For<DocumentView>());

            container.Register(Component.For<IWindowManager>().ImplementedBy<WindowManager>());
        }

        protected override object GetInstance(Type service, string key)
        {
            if (key != null)
                return container.Resolve(key, service);

            return container.Resolve(service);
        }

        protected override System.Collections.Generic.IEnumerable<object> GetAllInstances(Type service)
        {
            return container.ResolveAll(service).OfType<object>();
        }
    }
}