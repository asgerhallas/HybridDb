using System.Collections.ObjectModel;
using System.Linq;
using Castle.Core.Internal;
using HybridDb.Studio.Infrastructure;
using HybridDb.Studio.Infrastructure.ViewModels;
using HybridDb.Studio.Infrastructure.Views;
using HybridDb.Studio.Messages;
using HybridDb.Studio.Models;
using Microsoft.Win32;

namespace HybridDb.Studio.ViewModels
{
    public class MainViewModel : ViewModel, IHandle<RecentFilesUpdated>
    {
        private readonly IViewModelFactory viewModelFactory;
        private readonly IApplication application;
        private readonly ISettings settings;
        private readonly IEventAggregator bus;
        private readonly MotherOfAll mother;
        private string statusBarText;
        private IView activeTab;

        public MainViewModel(IViewModelFactory viewModelFactory, IApplication application, ISettings settings, IEventAggregator bus, MotherOfAll mother)
        {
            this.viewModelFactory = viewModelFactory;
            this.application = application;
            this.settings = settings;
            this.bus = bus;
            this.mother = mother;
            Tabs = new ObservableCollection<ViewModel>();
            Stores = new Stores();

            bus.Subscribe(this);

            StatusBarText = "Does it work?";

            OpenConnectionDialog = RelayCommand.Create(HandleOpenConnectionDialog);
            OpenRecent = RelayCommand.Create<string>(HandleOpenRecent);
            ExitApplication = RelayCommand.Create(HandleExitApplication);

            RecentlyOpened = new ObservableCollection<string>(settings.RecentFiles);

            CloseTab = RelayCommand.Create<ViewModel>(HandleCloseTab);
            CloseAllTabs = RelayCommand.Create(HandleCloseAllTabs);
            CloseAllTabsButThis = RelayCommand.Create<ViewModel>(HandleCloseAllTabsButThis);

            OpenTab<ListViewModel>();
            OpenTab<DocumentViewModel>();
        }

        private void HandleOpenConnectionDialog()
        {

        }

        public Stores Stores { get; private set; }

        public IRelayCommand OpenConnectionDialog { get; private set; }
        public IRelayCommand OpenRecent { get; private set; }
        public IRelayCommand ExitApplication { get; private set; }

        public IRelayCommand CloseTab { get; private set; }
        public IRelayCommand CloseAllTabs { get; private set; }
        public IRelayCommand CloseAllTabsButThis { get; private set; }


        public ObservableCollection<string> RecentlyOpened { get; private set; }

        public string StatusBarText
        {
            get { return statusBarText; }
            set
            {
                if (value == statusBarText) return;
                statusBarText = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<ViewModel> Tabs { get; private set; }

        public IView ActiveTab
        {
            get { return activeTab; }
            set
            {
                activeTab = value;
                OnPropertyChanged();
            }
        }

        public void OpenTab<TViewModel>() where TViewModel : ViewModel
        {
            Tabs.Add(viewModelFactory.Create<TViewModel>());
        }

        public void Handle(RecentFilesUpdated message)
        {
            RecentlyOpened.Clear();
            settings.RecentFiles.ForEach(RecentlyOpened.Add);
        }

        private void HandleOpenRecent(string filepath)
        {
            StatusBarText = "Åbn seneste " + filepath;
        }

        private void HandleOpenFile()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Assemblies (*.dll;*.exe)|*.dll;*.exe|All files (*.*)|*.*",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                StatusBarText = openFileDialog.FileName;

                var configurator = mother.GetConfiguratorFromAssembly(openFileDialog.FileName);
                var store = Stores.NewStore("bogus", configurator);

                QueryStats stats;
                var companies = store.DocumentStore.Query(store.DocumentStore.Configuration.Tables["Companies"], out stats);

                bus.Publish(new FileOpened
                {
                    Filepath = openFileDialog.FileName
                });
            }
        }

        private void HandleExitApplication()
        {
            application.Shutdown();
        }

        private void HandleCloseTab(ViewModel viewModel)
        {
            Tabs.Remove(viewModel);
        }

        private void HandleCloseAllTabsButThis(ViewModel viewModel)
        {
            foreach (var tab in Tabs.Where(x => x != viewModel).ToList())
            {
                Tabs.Remove(tab);
            }
        }

        private void HandleCloseAllTabs()
        {
            foreach (var tab in Tabs.ToList())
            {
                Tabs.Remove(tab);
            }
        }
    }
}