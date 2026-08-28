namespace WinTweaker.Models;

/// <summary>
/// 日志级别（对应四色显示）
/// </summary>
public enum LogLevel
{
    /// <summary>信息 - 黑色/白色(跟随主题)</summary>
    Info,
    /// <summary>成功 - 绿色</summary>
    Success,
    /// <summary>警告 - 黄色/橙色</summary>
    Warning,
    /// <summary>错误 - 红色</summary>
    Error
}

/// <summary>
/// 单条日志条目
/// </summary>
public sealed class LogEntry
{
    public DateTime Timestamp { get; init; }
    public LogLevel Level { get; init; }
    public string Message { get; init; } = string.Empty;

    public string FormattedTime => Timestamp.ToString("HH:mm:ss.fff");

    public string LevelTag => Level switch
    {
        LogLevel.Info => "[信息]",
        LogLevel.Success => "[成功]",
        LogLevel.Warning => "[警告]",
        LogLevel.Error => "[错误]",
        _ => "[未知]"
    };
}
