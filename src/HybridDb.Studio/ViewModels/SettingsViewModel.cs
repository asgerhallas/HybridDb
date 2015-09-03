using System;
using System.Data.SqlClient;
using System.Windows;
using Caliburn.Micro;
using HybridDb.Studio.Properties;

namespace HybridDb.Studio.ViewModels
{
    public class SettingsViewModel : Screen, ISettings
    {
        public SettingsViewModel()
        {
            DisplayName = "Setup connectionstring";
        }

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

            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void Save()
        {
            bool connectionIsValid = ConnectionIsValid();
            if (!connectionIsValid)
            {
                MessageBox.Show("Could not establish connection with connectionstring.", "Error", MessageBoxButton.OK);
                return;
            }

            Settings.Default.Save();
            TryClose(true);
        }
    }
}