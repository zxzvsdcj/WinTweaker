using WinTweaker.Services;

namespace WinTweaker.Models;

/// <summary>
/// 系统能力掩码 —— 开机时一次性判断所有功能可用性
/// UI 层直接绑定此模型控制置灰/提示
/// </summary>
public sealed class SystemCapabilities
{
    private readonly SystemInfo _sysInfo;

    public SystemCapabilities(SystemInfo sysInfo)
    {
        _sysInfo = sysInfo;
    }

    // ===== 通用优化 =====
    /// <summary>卓越性能电源计划（全版本可用）</summary>
    public bool CanUltimatePowerPlan => true;

    /// <summary>WSL2 配置（全版本可用）</summary>
    public bool CanWslConfig => true;

    /// <summary>遥测降级（全版本可用）</summary>
    public bool CanTelemetry => true;

    /// <summary>关闭广告（全版本可用）</summary>
    public bool CanDisableAds => true;

    /// <summary>裁剪冗余服务（全版本可用）</summary>
    public bool CanTrimServices => true;

    /// <summary>禁止后台运行（全版本可用）</summary>
    public bool CanDisableBackground => true;

    /// <summary>关闭休眠（全版本可用）</summary>
    public bool CanDisableHibernation => true;

    /// <summary>资源管理器优化（全版本可用）</summary>
    public bool CanExplorerTweaks => true;

    /// <summary>阻止静默安装推荐应用（全版本可用）</summary>
    public bool CanBlockSilentInstall => true;

    /// <summary>关闭锁屏/开始菜单广告（全版本可用）</summary>
    public bool CanDisableLockScreenAds => true;

    // ===== Win11 专属功能 =====
    /// <summary>禁用 Copilot（仅Win11）</summary>
    public bool CanDisableCopilot => _sysInfo.IsWindows11;

    /// <summary>禁用 Widgets 小组件（仅Win11）</summary>
    public bool CanDisableWidgets => _sysInfo.IsWindows11;

    /// <summary>关闭任务栏 Bing 搜索（仅Win11）</summary>
    public bool CanDisableTaskbarBing => _sysInfo.IsWindows11;

    // ===== 高危操作 =====
    /// <summary>关闭 UAC（全版本可用）</summary>
    public bool CanDisableUac => true;

    /// <summary>关闭防火墙（全版本可用）</summary>
    public bool CanDisableFirewall => true;

    /// <summary>
    /// 压制 Defender 实时防护
    /// 家庭版不支持组策略，但可通过注册表操作
    /// 新版 Win11 24H2+ 策略可能被系统自动重置
    /// </summary>
    public bool CanSuppressDefender => true;

    /// <summary>Defender 策略是否会被系统限制（24H2+新版问题）</summary>
    public bool DefenderPolicyMayBeRestricted => _sysInfo.IsNewWin11DefenderRestricted;

    /// <summary>Defender 策略是否需要通过注册表而非组策略（家庭版）</summary>
    public bool DefenderRequiresRegistryOnly => !_sysInfo.SupportsGroupPolicy;

    /// <summary>是否为预览版（需特殊提示安全策略可能被重置）</summary>
    public bool IsInsiderWithResetRisk => _sysInfo.IsInsiderPreview;

    // ===== UI 材质 =====
    /// <summary>是否支持 Mica 材质</summary>
    public bool SupportsMica => _sysInfo.SupportsMica;

    /// <summary>
    /// 获取功能不可用时的提示文本
    /// </summary>
    public string GetUnavailableReason(string featureId)
    {
        return featureId switch
        {
            "Copilot" => "此功能仅适用于 Windows 11",
            "Widgets" => "此功能仅适用于 Windows 11",
            "TaskbarBing" => "此功能仅适用于 Windows 11",
            _ => "当前系统版本不支持此功能"
        };
    }

    /// <summary>
    /// 获取高危操作的额外警告文本
    /// </summary>
    public string? GetDangerWarning(string featureId)
    {
        return featureId switch
        {
            "Defender" when DefenderPolicyMayBeRestricted =>
                $"⚠ 当前系统为 Win11 {_sysInfo.DisplayVersion} (Build {_sysInfo.BuildNumber})，" +
                "新版系统的安全策略可能在更新后被自动重置，效果不持久。",
            "Defender" when IsInsiderWithResetRisk =>
                "⚠ 当前为 Insider 预览版，安全策略会被系统自动重置，修改可能无法长期生效。",
            "Defender" when DefenderRequiresRegistryOnly =>
                "⚠ 当前为家庭版，不支持组策略，将通过注册表方式操作。",
            _ => null
        };
    }
}
