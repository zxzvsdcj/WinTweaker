namespace WinTweaker.Models;

/// <summary>
/// Windows 主版本标识
/// </summary>
public enum WindowsMajorVersion
{
    Unsupported = 0,
    Windows10 = 10,
    Windows11 = 11
}

/// <summary>
/// Windows SKU 版本类型
/// </summary>
public enum WindowsEdition
{
    Unknown = 0,
    Home = 1,
    Professional = 2,
    Enterprise = 3,
    Education = 4
}

/// <summary>
/// 系统信息模型 —— 承载所有版本检测结果
/// </summary>
public sealed class SystemInfo
{
    /// <summary>主版本：Win10 / Win11 / 不支持</summary>
    public WindowsMajorVersion MajorVersion { get; init; }

    /// <summary>OS Build 号，如 22631、26200</summary>
    public int BuildNumber { get; init; }

    /// <summary>显示版本号，如 "23H2"、"24H2"</summary>
    public string DisplayVersion { get; init; } = string.Empty;

    /// <summary>SKU 版本：家庭版/专业版/企业版/教育版</summary>
    public WindowsEdition Edition { get; init; }

    /// <summary>是否为 Insider 预览版</summary>
    public bool IsInsiderPreview { get; init; }

    /// <summary>完整版本字符串，如 "Windows 11 专业版 23H2 (Build 22631)"</summary>
    public string FullVersionString { get; init; } = string.Empty;

    /// <summary>是否为 Win11（快捷判断）</summary>
    public bool IsWindows11 => MajorVersion == WindowsMajorVersion.Windows11;

    /// <summary>是否为 Win10</summary>
    public bool IsWindows10 => MajorVersion == WindowsMajorVersion.Windows10;

    /// <summary>是否支持 Mica 材质 (Win11 Build >= 22000)</summary>
    public bool SupportsMica => IsWindows11 && BuildNumber >= 22000;

    /// <summary>是否支持组策略 (非家庭版)</summary>
    public bool SupportsGroupPolicy => Edition != WindowsEdition.Home;

    /// <summary>
    /// 是否为新版 Win11 (24H2+)，Defender 策略可能被系统限制
    /// Build 26100+ 为 24H2 正式版
    /// </summary>
    public bool IsNewWin11DefenderRestricted => IsWindows11 && BuildNumber >= 26100;
}
