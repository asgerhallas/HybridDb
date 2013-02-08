using System;
using System.Data.SqlClient;
using System.Windows;
using Caliburn.Micro;
using HybridDb.Studio.Properties;

namespace HybridDb.Studio.ViewModels
{
    public class SettingsViewModel : Screen, ISettings
    {
        public string ConnectionString
        {
            get { return (string) Settings.Default["ConnectionString"]; }
            set
            {
                Settings.Default["ConnectionString"] = value;
                NotifyOfPropertyChange(() => ConnectionString);
            }
        }

        public bool ConnectionIsValid()
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
                return false;

            using (var store = new DocumentStore(ConnectionString))
            {
                return store.CanConnect();
            }
        }

        public override void CanClose(Action<bool> callback)
        {
            bool connectionIsValid = ConnectionIsValid();
            if (!connectionIsValid)
            {
                MessageBoxResult result = MessageBox.Show("Could not establish connection with connectionstring.", "Error", MessageBoxButton.OKCancel);
                if (result != MessageBoxResult.OK)
                {
                    App.Current.Shutdown();
                    return;
                }
            }
            
            callback(connectionIsValid);
        }

        public void Save()
        {
            if (!ConnectionIsValid())
            {
                MessageBoxResult result = MessageBox.Show("Could not establish connection with connectionstring.", "Error", MessageBoxButton.OKCancel);
                if (result != MessageBoxResult.OK)
                {
                    App.Current.Shutdown();
                }
                return;
            }

            Settings.Default.Save();
            TryClose(true);
        }
    }
}