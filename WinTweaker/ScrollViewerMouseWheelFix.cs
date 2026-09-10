using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WinTweaker;

/// <summary>
/// 让页面 ScrollViewer 在内容区与滚动条区域都能响应滚轮；
/// 并在 NavigationView 边距空白处将滚轮转发给当前页滚动容器。
/// </summary>
internal static class ScrollViewerMouseWheelFix
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
            return;

        EventManager.RegisterClassHandler(
            typeof(ScrollViewer),
            UIElement.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(OnScrollViewerPreviewMouseWheel),
            handledEventsToo: false);

        EventManager.RegisterClassHandler(
            typeof(UIElement),
            UIElement.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(OnUiElementPreviewMouseWheel),
            handledEventsToo: false);

        _registered = true;
    }

    private static void OnScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || sender is not ScrollViewer sv || sv.ScrollableHeight <= 0)
            return;

        if (e.OriginalSource is DependencyObject src &&
            FindAncestorScrollViewer(src) is { } inner &&
            !ReferenceEquals(inner, sv) &&
            inner.ScrollableHeight > 0)
            return;

        ApplyWheel(sv, e);
    }

    private static void OnUiElementPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled)
            return;

        // 已在可滚动 ScrollViewer 内则交给上面的 handler
        if (e.OriginalSource is DependencyObject src &&
            FindAncestorScrollViewer(src) is { ScrollableHeight: > 0 })
            return;

        // 鼠标落在边距空白等非 ScrollViewer 区域：转发到当前窗口内第一个可滚的页面 ScrollViewer
        if (Application.Current?.MainWindow is not { IsLoaded: true } window)
            return;

        var pageScroller = FindDescendantScrollViewer(window);
        if (pageScroller is null || pageScroller.ScrollableHeight <= 0)
            return;

        ApplyWheel(pageScroller, e);
    }

    private static void ApplyWheel(ScrollViewer sv, MouseWheelEventArgs e)
    {
        var lines = SystemParameters.WheelScrollLines;
        if (lines <= 0)
            lines = 3;
        var offset = e.Delta / 120.0 * lines * 16.0;
        sv.ScrollToVerticalOffset(sv.VerticalOffset - offset);
        e.Handled = true;
    }

    private static ScrollViewer? FindAncestorScrollViewer(DependencyObject? current)
    {
        while (current != null)
        {
            if (current is ScrollViewer sv)
                return sv;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static ScrollViewer? FindDescendantScrollViewer(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer { ScrollableHeight: > 0 } sv)
                return sv;

            var nested = FindDescendantScrollViewer(child);
            if (nested != null)
                return nested;
        }
        return null;
    }
}
