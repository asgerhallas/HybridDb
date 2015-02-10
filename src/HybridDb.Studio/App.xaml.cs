using System.Windows;
using Castle.Facilities.TypedFactory;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using HybridDb.Studio.ViewModels;

namespace HybridDb.Studio
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
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
                .Register(Component.For<IViewLocator>().AsFactory());

            var mainWindow = container.Resolve<MainWindow>();
            mainWindow.Show();
        }
    }

    public interface IView
    {
    }
}
