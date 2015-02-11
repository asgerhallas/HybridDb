using System.Windows;
using Castle.Facilities.TypedFactory;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using HybridDb.Studio.Infrastructure;
using HybridDb.Studio.Infrastructure.ViewModels;
using HybridDb.Studio.Infrastructure.Views;
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
                .Register(
                    Types.FromThisAssembly()
                        .BasedOn<IView>()
                        .WithServiceSelf())
                .Register(
                    Types.FromThisAssembly()
                        .BasedOn<ViewModel>()
                        .WithServiceSelf())
                .Register(Component.For<IViewModelFactory>().AsFactory())
                .Register(Component.For<IApplication>().Instance(this))
                .Register(Component.For<ISettings>().ImplementedBy<ApplicationSettingsAdapter>().LifestyleSingleton())
                .Register(Component.For<IEventAggregator>().ImplementedBy<EventAggregator>().LifestyleSingleton());

            var mainWindow = container.Resolve<MainWindow>();
            mainWindow.Show();
        }
    }
}
