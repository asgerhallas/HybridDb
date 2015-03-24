using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace HybridDb.Studio.Controls
{
    public class TabContentSelector : DataTemplateSelector
    {
        public TabContentSelector()
        {
            DataTemplates = new Dictionary<string, DataTemplate>();
        }

        public Dictionary<string, DataTemplate> DataTemplates { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item == null)
                return null;

            return DataTemplates[item.GetType().Name];
        }
    }
}