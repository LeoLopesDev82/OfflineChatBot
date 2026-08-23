using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace OfflineChatBot.Helpers
{
    public static class VisualTreeExtensions
    {
        public static T? FindParent<T>(this DependencyObject? element) where T : DependencyObject
        {
            var current = element;

            while (current != null)
            {
                if (current is T match)
                    return match;

                current = GetParent(current);
            }

            return null;
        }

        #region Private Methods

        private static DependencyObject? GetParent(DependencyObject element)
        {
            var logicalParent = LogicalTreeHelper.GetParent(element);

            if (logicalParent != null)
                return logicalParent;

            return element is Visual or Visual3D ? VisualTreeHelper.GetParent(element) : null;
        }

        #endregion
    }
}