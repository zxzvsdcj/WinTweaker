using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using WinTweaker.License;
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

        if (!LicenseGate.IsLicensed)
            ShowLicenseLockBanner();
    }

    private void ShowLicenseLockBanner()
    {
        RootNavigation.IsEnabled = false;
        var result = LicenseGate.LastResult;
        var code = result?.MachineCode ?? MachineCode.Generate();
        var msg = result?.Message ?? "本机尚未激活";

        if (Content is not Grid grid)
            return;

        var banner = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xEE, 0x1A, 0x1A, 0x1A)),
            Padding = new Thickness(16),
            VerticalAlignment = VerticalAlignment.Bottom,
            Child = new StackPanel
            {
                Children =
                {
                    new System.Windows.Controls.TextBlock
                    {
                        Text = "核心功能已锁定 — " + msg,
                        Foreground = Brushes.Orange,
                        FontWeight = FontWeights.SemiBold,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 6),
                    },
                    new System.Windows.Controls.TextBlock
                    {
                        Text = "机器码: " + code,
                        Foreground = Brushes.White,
                        FontFamily = new FontFamily("Consolas"),
                        TextWrapping = TextWrapping.Wrap,
                    },
                },
            },
        };
        Grid.SetRowSpan(banner, 2);
        Panel.SetZIndex(banner, 100);
        grid.Children.Add(banner);
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
