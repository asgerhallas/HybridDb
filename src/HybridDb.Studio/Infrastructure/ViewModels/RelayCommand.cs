using System;

namespace HybridDb.Studio.Infrastructure.ViewModels
{
    public static class RelayCommand
    {
        public static IRelayCommand Create(Action execute)
        {
            return new RelayCommandWithParameter<object>(
                o => AssertNoParameterUsage(o, execute), 
                o => true);
        }

        public static IRelayCommand Create(Action execute, Func<bool> canExecute)
        {
            return new RelayCommandWithParameter<object>(
                o => AssertNoParameterUsage(o, execute), 
                o => AssertNoParameterUsage(o, canExecute));
        }

        public static IRelayCommand Create<TParam>(Action<TParam> execute)
        {
            return new RelayCommandWithParameter<TParam>(execute, o => true);
        }

        public static IRelayCommand Create<TParam>(Action<TParam> execute, Predicate<TParam> canExecute)
        {
            return new RelayCommandWithParameter<TParam>(execute, canExecute);
        }

        static void AssertNoParameterUsage(object parameter, Action action)
        {
            if (parameter != null)
                throw new NotSupportedException("RelayCommand not created with parameter, but parameter is supplied by command.");

            action();
        }

        static bool AssertNoParameterUsage(object parameter, Func<bool> predicate)
        {
            if (parameter != null)
                throw new NotSupportedException("RelayCommand not created with parameter, but parameter is supplied by command.");

            return predicate();
        }

        class RelayCommandWithParameter<TParam> : IRelayCommand
        {
            private readonly Predicate<TParam> canExecute;
            private readonly Action<TParam> execute;

            public RelayCommandWithParameter(Action<TParam> execute, Predicate<TParam> canExecute)
            {
                this.execute = execute;
                this.canExecute = canExecute;
            }

            public bool CanExecute(object parameter)
            {
                return canExecute((TParam) parameter);
            }

            public void Execute(object parameter)
            {
                execute((TParam)parameter);
            }

            public event EventHandler CanExecuteChanged;

            public void RaiseCanExecuteChanged()
            {
                if (CanExecuteChanged != null)
                {
                    CanExecuteChanged(this, EventArgs.Empty);
                }
            }
        }
    }
}