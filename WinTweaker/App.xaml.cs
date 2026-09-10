using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using WinTweaker.License;
using WinTweaker.Models;
using WinTweaker.Services;
using WinTweaker.Views;

namespace WinTweaker;

public partial class App : Application
{
    private static readonly string CrashLogPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "crash.log");

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);

    private const int AttachParentProcess = -1;

    protected override void OnStartup(StartupEventArgs e)
    {
        // CLI：--license-check [--online] 冒烟后退出（不弹主窗）
        if (e.Args.Any(a => string.Equals(a, "--license-check", StringComparison.OrdinalIgnoreCase)))
        {
            if (!AttachConsole(AttachParentProcess))
                AllocConsole();
            RunLicenseCli(e.Args);
            Shutdown(Environment.ExitCode);
            return;
        }

        base.OnStartup(e);

        // 内容区任意位置均可滚轮滚动（无需移到右侧滚动条）
        ScrollViewerMouseWheelFix.Register();

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

            // WuGuard 授权校验（直连飞书，不依赖 8090）
            var license = LicenseClient.CheckLicense();
            LicenseGate.SetResult(license);
            log.Info($"[授权] status={license.Status} product=win-tweaker machine={license.MachineCode}");
            if (!string.IsNullOrEmpty(license.Message))
                log.Info($"[授权] {license.Message}");

            if (!license.Ok)
            {
                var activation = new ActivationWindow(license);
                activation.ShowDialog();
            }

            var main = new MainWindow();
            MainWindow = main;
            main.Show();
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
            System.Windows.MessageBox.Show($"启动失败：{ex.Message}\n\n详情已写入 crash.log",
                "启动错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static void RunLicenseCli(string[] args)
    {
        var online = args.Any(a => string.Equals(a, "--online", StringComparison.OrdinalIgnoreCase));
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine(new string('=', 60));
        Console.WriteLine("  WuGuard / WinTweaker License 冒烟");
        Console.WriteLine(new string('=', 60));

        var guid = MachineCode.GetMachineGuid();
        var host = MachineCode.GetComputerName();
        var code = MachineCode.Generate();
        Console.WriteLine($"  MachineGuid : {guid}");
        Console.WriteLine($"  ComputerName: {host}");
        Console.WriteLine($"  machine_code: {code}");
        Console.WriteLine($"  product_code: win-tweaker");

        if (code.Length != 64 || code.Any(c => c is < '0' or > '9' and (< 'a' or > 'f')))
        {
            Console.WriteLine("  FAIL: 机器码格式");
            Environment.ExitCode = 1;
            return;
        }
        Console.WriteLine("  PASS: 机器码格式");

        if (MachineCode.Generate() != code)
        {
            Console.WriteLine("  FAIL: 机器码不稳定");
            Environment.ExitCode = 1;
            return;
        }
        Console.WriteLine("  PASS: 机器码稳定");

        if (!online)
        {
            Console.WriteLine();
            Console.WriteLine("  （未加 --online，跳过飞书在线校验）");
            Console.WriteLine("  结果: 本地用例通过");
            Environment.ExitCode = 0;
            return;
        }

        var result = LicenseClient.CheckLicense();
        LicenseGate.SetResult(result);
        Console.WriteLine();
        Console.WriteLine($"  status       : {result.Status}");
        Console.WriteLine($"  message      : {result.Message}");
        Console.WriteLine($"  license_type : {result.LicenseType}");
        Console.WriteLine($"  remaining    : {result.RemainingDays}");
        Console.WriteLine($"  ok           : {result.Ok}");
        if (result.Status == "not_configured")
        {
            Console.WriteLine("  FAIL: 配置无效");
            Environment.ExitCode = 1;
            return;
        }
        Console.WriteLine("  PASS: 在线链路已执行");
        Environment.ExitCode = 0;
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
