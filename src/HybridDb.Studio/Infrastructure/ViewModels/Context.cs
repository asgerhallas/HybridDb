using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using HybridDb.Studio.Infrastructure.Views;

namespace HybridDb.Studio.Infrastructure.ViewModels
{
    public class Context
    {
        public static DependencyProperty ViewModelProperty =
            DependencyProperty.RegisterAttached(
                "Model",
                typeof(object),
                typeof(Context),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnViewModelChanged)
                );

        public static double GetViewModel(DependencyObject obj)
        {
            return (double)obj.GetValue(ViewModelProperty);
        }

        public static void SetViewModel(DependencyObject obj, double value)
        {
            obj.SetValue(ViewModelProperty, value);
        }

        public static readonly DependencyProperty ViewModelsProperty =
            DependencyProperty.RegisterAttached(
                "ViewModels",
                typeof(object),
                typeof (Context),
                new PropertyMetadata(null, OnViewModelsChanged));

        public static void SetViewModels(DependencyObject element, object value)
        {
            element.SetValue(ViewModelsProperty, value);
        }

        public static object GetViewModels(DependencyObject element)
        {
            return element.GetValue(ViewModelsProperty);
        }

        private static void OnViewModelsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var itemsControl = d as ItemsControl;
            if (itemsControl == null)
                throw new InvalidOperationException("Attached property, Context.ViewModels, can only be set on ItemsControl.");

            if (e.NewValue != null)
            {
                var viewModel = (ViewModel)e.NewValue;
                var view = ViewLocator.LocateView(viewModel);
                view.DataContext = viewModel;

                contentControl.Content = view;
            }
            else
            {
                contentControl.Content = null;
            }
        }

        private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var contentControl = d as ContentControl;
            if (contentControl == null)
                throw new InvalidOperationException("Attached property, Context.ViewModel, can only be set on ContentControl.");

            if (e.NewValue != null)
            {
                var viewModel = (ViewModel) e.NewValue;
                var view = ViewLocator.LocateView(viewModel);
                view.DataContext = viewModel;

                contentControl.Content = view;
            }
            else
            {
                contentControl.Content = null;
            }
        }
    }
}