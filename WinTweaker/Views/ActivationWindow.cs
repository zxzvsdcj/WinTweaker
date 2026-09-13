using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinTweaker.License;

namespace WinTweaker.Views;

/// <summary>
/// 未授权时展示机器码的激活引导窗。
/// </summary>
public partial class ActivationWindow : Window
{
    public ActivationWindow(LicenseCheckResult result)
    {
        Title = "WinTweaker 授权激活";
        Width = 560;
        Height = 360;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));

        var root = new StackPanel { Margin = new Thickness(28) };

        root.Children.Add(new TextBlock
        {
            Text = "需要授权后才能使用核心功能",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 12),
        });

        root.Children.Add(new TextBlock
        {
            Text = result.Message,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            Margin = new Thickness(0, 0, 0, 16),
        });

        root.Children.Add(new TextBlock
        {
            Text = "本机机器码（发给管理员录入）",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
            Margin = new Thickness(0, 0, 0, 6),
        });

        var codeBox = new TextBox
        {
            Text = result.MachineCode,
            IsReadOnly = true,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 0, 12),
        };
        root.Children.Add(codeBox);

        root.Children.Add(new TextBlock
        {
            Text = "product_code: win-tweaker",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
            Margin = new Thickness(0, 0, 0, 18),
        });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var copyBtn = new Button
        {
            Content = "复制机器码",
            Width = 120,
            Height = 32,
            Margin = new Thickness(0, 0, 8, 0),
        };
        copyBtn.Click += (_, _) =>
        {
            Clipboard.SetText(result.MachineCode);
            copyBtn.Content = "已复制";
        };

        var closeBtn = new Button
        {
            Content = "进入（功能已锁定）",
            Width = 150,
            Height = 32,
            IsDefault = true,
        };
        closeBtn.Click += (_, _) => Close();

        buttons.Children.Add(copyBtn);
        buttons.Children.Add(closeBtn);
        root.Children.Add(buttons);

        Content = root;
    }
}
