using System.Windows;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using WinTweaker.Services;
using WinTweaker.ViewModels;

namespace WinTweaker.Views;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();

        Icon = new System.Windows.Media.Imaging.BitmapImage(
            new Uri("pack://application:,,,/Assets/window.ico", UriKind.Absolute));

        var sysInfo = SystemInfoService.Instance.Current;
        WindowBackdropType = sysInfo.SupportsMica
            ? WindowBackdropType.Mica
            : WindowBackdropType.Acrylic;

        SystemThemeWatcher.Watch(this, WindowBackdropType);

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RootNavigation.SetPageProviderService(new SimplePageProvider());

        // 默认导航到"常规优化"页面，避免右侧空白
        RootNavigation.Navigate(typeof(GeneralPage));
    }
}

internal sealed class SimplePageProvider : INavigationViewPageProvider
{
    private readonly Dictionary<Type, object> _cache = new();

    public object? GetPage(Type pageType)
    {
        if (!_cache.TryGetValue(pageType, out var page))
        {
            page = Activator.CreateInstance(pageType);
            if (page != null)
                _cache[pageType] = page;
        }
        return page;
    }
}
