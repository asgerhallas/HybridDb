using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using HybridDb.Studio.Messages;

namespace HybridDb.Studio.Infrastructure
{
    public class ApplicationSettingsAdapter : ISettings, IHandle<FileOpened>
    {
        private readonly IEventAggregator bus;

        public ApplicationSettingsAdapter(IEventAggregator bus)
        {
            this.bus = bus;
            bus.Subscribe(this);

            if (Properties.Settings.Default.RecentFiles == null)
            {
                Properties.Settings.Default.RecentFiles = new StringCollection();
                Save();
            }
        }

        public IReadOnlyList<string> RecentFiles
        {
            get { return Properties.Settings.Default.RecentFiles.OfType<string>().ToList(); }
        }

        public void Save()
        {
            Properties.Settings.Default.Save();
        }

        public void Handle(FileOpened message)
        {
            var recentFiles = Properties.Settings.Default.RecentFiles;

            var indexOfExisting = recentFiles.IndexOf(message.Filepath);
            if (indexOfExisting != -1)
            {
                recentFiles.RemoveAt(indexOfExisting);
            }

            if (recentFiles.Count > 5)
            {
                recentFiles.RemoveAt(recentFiles.Count-1);
            }

            recentFiles.Insert(0, message.Filepath);
            Properties.Settings.Default.Save();

            bus.Publish(new RecentFilesUpdated());
        }
    }
}