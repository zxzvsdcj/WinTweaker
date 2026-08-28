using System.IO;
using System.Windows;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using WinTweaker.Models;
using WinTweaker.Services;

namespace WinTweaker;

public partial class App : Application
{
    private static readonly string CrashLogPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "crash.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 全局异常捕获
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        try
        {
            var sysInfo = SystemInfoService.Instance;
            var (isSupported, errorMessage) = sysInfo.ValidateSystemSupport();

            if (!isSupported)
            {
                System.Windows.MessageBox.Show(errorMessage, "系统不兼容",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            // 根据系统版本选择材质并应用主题
            var backdropType = sysInfo.Current.SupportsMica
                ? WindowBackdropType.Mica
                : WindowBackdropType.Acrylic;

            ApplicationThemeManager.Apply(
                ApplicationTheme.Dark,
                backdropType,
                updateAccent: true
            );

            var log = LogService.Instance;
            log.Info($"系统识别：{sysInfo.Current.FullVersionString}");
            log.Info($"窗口材质：{(sysInfo.Current.SupportsMica ? "Mica 云母" : "Acrylic 亚克力降级")}");

            if (sysInfo.Current.IsInsiderPreview)
            {
                log.Warning("当前为 Insider 预览版，部分安全策略修改可能被系统自动重置");
            }

            if (sysInfo.Current.IsNewWin11DefenderRestricted)
            {
                log.Warning($"Win11 {sysInfo.Current.DisplayVersion} 已限制第三方策略关闭 Defender，相关功能效果可能不持久");
            }
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
            System.Windows.MessageBox.Show($"启动失败：{ex.Message}\n\n详情已写入 crash.log",
                "启动错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception);
        e.Handled = true;
        System.Windows.MessageBox.Show($"未处理异常：{e.Exception.Message}\n\n详情已写入 crash.log",
            "运行时错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            WriteCrashLog(ex);
    }

    private static void WriteCrashLog(Exception ex)
    {
        try
        {
            string content = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{ex}\n\n";
            File.AppendAllText(CrashLogPath, content);
        }
        catch { /* 日志写入失败不阻塞 */ }
    }
}
