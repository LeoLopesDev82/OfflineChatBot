using System.Windows;
using System.Windows.Controls;

namespace OfflineChatBot.Behaviors
{
    public static class AutoScrollBehavior
    {
        public static readonly DependencyProperty ScrollToEndProperty =
            DependencyProperty.RegisterAttached(
                "ScrollToEnd",
                typeof(bool),
                typeof(AutoScrollBehavior),
                new PropertyMetadata(false, OnScrollToEndChanged));

        public static bool GetScrollToEnd(DependencyObject element)
        {
            return (bool)element.GetValue(ScrollToEndProperty);
        }

        public static void SetScrollToEnd(DependencyObject element, bool value)
        {
            element.SetValue(ScrollToEndProperty, value);
        }

        #region Private Methods

        private static void OnScrollToEndChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
        {
            if (element is not ScrollViewer scrollViewer)
                return;

            scrollViewer.ScrollChanged -= OnScrollChanged;

            if (e.NewValue is not true)
                return;

            scrollViewer.ScrollChanged += OnScrollChanged;
        }

        private static void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.ExtentHeightChange == 0)
                return;

            ((ScrollViewer)sender).ScrollToEnd();
        }

        #endregion
    }
}