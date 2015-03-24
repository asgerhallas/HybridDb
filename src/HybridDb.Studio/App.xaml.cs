using System.Windows;
using Castle.Facilities.TypedFactory;
using Castle.MicroKernel;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using HybridDb.Studio.Infrastructure;
using HybridDb.Studio.Infrastructure.ViewModels;
using HybridDb.Studio.Infrastructure.Views;
using HybridDb.Studio.Infrastructure.Windsor;
using HybridDb.Studio.ViewModels;
using HybridDb.Studio.Views;

namespace HybridDb.Studio
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, IApplication
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            var container = new WindsorContainer()
                .AddFacility<TypedFactoryFacility>()
                .Register(Component.For<FirstArgumentIsNameSelector>())
                .Register(
                    Types.FromThisAssembly()
                        .BasedOn<IView>()
                        .If(x => !x.IsAbstract)
                        .Configure(x => x.Named(x.Implementation.Name))
                        .WithServiceBase()
                        .LifestyleTransient())
                .Register(
                    Types.FromThisAssembly()
                        .BasedOn<ViewModel>()
                        .WithServiceSelf())
                .Register(Component.For<WindowManager>())
                .Register(Component.For<IViewModelFactory>().AsFactory())
                .Register(Component.For<IViewFactory>().AsFactory(x => x.SelectedWith<FirstArgumentIsNameSelector>()))
                .Register(Component.For<IApplication>().Instance(this))
                .Register(Component.For<MotherOfAll>())
                .Register(Component.For<ISettings>().ImplementedBy<ApplicationSettingsAdapter>().LifestyleSingleton())
                .Register(Component.For<IEventAggregator>().ImplementedBy<EventAggregator>().LifestyleSingleton());

            ViewLocator.ViewFactory = s => container.Resolve<IViewFactory>().CreateView(s);

            var windowManager = container.Resolve<WindowManager>();
            windowManager.OpenWindow<MainViewModel>();
        }
    }
}
