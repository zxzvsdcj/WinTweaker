using System.Collections.ObjectModel;
using WinTweaker.Models;

namespace WinTweaker.Services;

/// <summary>
/// 日志服务 —— 四色分级、带时间戳
/// 支持 UI 绑定 ObservableCollection
/// </summary>
public sealed class LogService
{
    private static readonly Lazy<LogService> _instance = new(() => new LogService());
    public static LogService Instance => _instance.Value;

    /// <summary>UI 绑定的日志集合（自动通知变更）</summary>
    public ObservableCollection<LogEntry> Logs { get; } = new();

    private readonly object _lock = new();

    private LogService() { }

    public void Info(string message) => AddLog(LogLevel.Info, message);
    public void Success(string message) => AddLog(LogLevel.Success, message);
    public void Warning(string message) => AddLog(LogLevel.Warning, message);
    public void Error(string message) => AddLog(LogLevel.Error, message);

    /// <summary>
    /// 版本不支持警告（自动附加系统版本信息）
    /// </summary>
    public void VersionNotSupported(string feature, string reason)
    {
        Warning($"[{feature}] 当前系统不支持此功能：{reason}");
    }

    /// <summary>
    /// 家庭版策略无效警告
    /// </summary>
    public void HomeEditionPolicyWarning(string feature)
    {
        Warning($"[{feature}] 当前为家庭版，组策略不可用，已切换为注册表方式操作");
    }

    /// <summary>
    /// 预览版重置风险提示
    /// </summary>
    public void InsiderResetRisk(string feature)
    {
        Warning($"[{feature}] 当前为 Insider 预览版，安全策略可能在系统更新后被自动重置");
    }

    /// <summary>
    /// 新版 Win11 Defender 策略废弃提示
    /// </summary>
    public void DefenderPolicyDeprecated(int buildNumber, string displayVersion)
    {
        Warning($"[Defender] Win11 {displayVersion} (Build {buildNumber}) 已限制第三方通过策略关闭实时防护，" +
                "修改可能被系统自动恢复。建议通过 Windows 安全中心手动管理。");
    }

    public void Clear()
    {
        lock (_lock)
        {
            // 确保在 UI 线程执行
            System.Windows.Application.Current?.Dispatcher.Invoke(() => Logs.Clear());
        }
    }

    /// <summary>获取全部日志文本（用于复制）</summary>
    public string GetAllText()
    {
        lock (_lock)
        {
            return string.Join(Environment.NewLine,
                Logs.Select(l => $"{l.FormattedTime} {l.LevelTag} {l.Message}"));
        }
    }

    private void AddLog(LogLevel level, string message)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message
        };

        lock (_lock)
        {
            // 确保在 UI 线程添加
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
            {
                Logs.Add(entry);
            }
            else
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() => Logs.Add(entry));
            }
        }
    }
}
