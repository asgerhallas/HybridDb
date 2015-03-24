using System;
using HybridDb.Studio.Infrastructure.ViewModels;

namespace HybridDb.Studio.Infrastructure.Views
{
    public static class ViewLocator
    {
        public static Func<string, IView> ViewFactory { get; set; }

        public static IView LocateView(ViewModel viewModel)
        {
            var viewModelName = viewModel.GetType().Name;

            const string suffix = "ViewModel";
            if (!viewModelName.EndsWith(suffix))
                throw new InvalidOperationException(String.Format("ViewModel must end with 'ViewModel' in order to locate view by convention. {0}", viewModelName));

            var viewName = viewModelName
                .Substring(0, viewModelName.Length - suffix.Length) +
                "View";

            return ViewFactory(viewName);
        }
    }
}