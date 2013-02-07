using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Windows;
using Caliburn.Micro;
using HybridDb.Studio.Properties;

namespace HybridDb.Studio.ViewModels
{
    public interface ISettings
    {
        bool ConnectionIsValid();
        string ConnectionString { get; set; }
    }

    public class SettingsViewModel : Screen, ISettings
    {
        public SettingsViewModel()
        {
        }

        public string ConnectionString
        {
            get
            {
                return (string)Settings.Default["ConnectionString"];
            }
            set
            {
                Settings.Default["ConnectionString"] = value;
                NotifyOfPropertyChange(() => ConnectionString);
            }
        }

        public bool ConnectionIsValid()
        {
            if (ConnectionString == null)
                return false;

            try { new DocumentStore(ConnectionString).Connect().Dispose(); }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (SqlException)
            {
                return false;
            }

            return true;
        }

        public override void CanClose(Action<bool> callback)
        {
            bool connectionIsValid = ConnectionIsValid();
            if (!connectionIsValid)
            {
                MessageBox.Show("Could not establish connection with connectionstring.", "Error", MessageBoxButton.OK);
            }
            
            callback(connectionIsValid);
        }

        public void Save()
        {
            if (ConnectionIsValid())
            {
                Settings.Default.Save();
                TryClose(true);
            }
        }
    }
}