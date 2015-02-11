using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HybridDb.Studio.Properties;

namespace HybridDb.Studio.Infrastructure.ViewModels
{
    public abstract class ViewModel : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        public virtual void Dispose()
        {
        }
    }
}