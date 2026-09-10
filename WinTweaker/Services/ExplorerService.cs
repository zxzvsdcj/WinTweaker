using System.Diagnostics;
using WinTweaker.License;

namespace WinTweaker.Services;

/// <summary>
/// 资源管理器控制 —— 纯 C# 重启 explorer，使壳层注册表改动立即生效
/// </summary>
public sealed class ExplorerService
{
    private static readonly Lazy<ExplorerService> _instance = new(() => new ExplorerService());
    public static ExplorerService Instance => _instance.Value;

    private readonly LogService _log = LogService.Instance;

    private ExplorerService() { }

    /// <summary>
    /// 结束并重新启动 explorer.exe
    /// </summary>
    public bool Restart()
    {
        if (!LicenseGate.EnsureLicensed(out var deny))
        {
            _log.Error($"[授权] 已阻断 Explorer 重启：{deny}");
            return false;
        }
        try
        {
            foreach (var process in Process.GetProcessesByName("explorer"))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(8000);
                }
                catch (Exception ex)
                {
                    _log.Warning($"结束 explorer 进程时出现问题：{ex.Message}");
                }
                finally
                {
                    process.Dispose();
                }
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "explorer.exe"),
                UseShellExecute = true
            });

            _log.Success("[Explorer] 资源管理器已重启");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"重启资源管理器失败：{ex.Message}");
            return false;
        }
    }
}
