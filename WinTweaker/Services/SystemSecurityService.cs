using Microsoft.Win32;
using WinTweaker.Models;

namespace WinTweaker.Services;

/// <summary>
/// 系统安全服务 —— UAC / Defender / 防火墙
/// 自动适配新版 Win11 策略限制并日志明确提示
/// </summary>
public sealed class SystemSecurityService
{
    private static readonly Lazy<SystemSecurityService> _instance = new(() => new SystemSecurityService());
    public static SystemSecurityService Instance => _instance.Value;

    private readonly RegistryService _reg = RegistryService.Instance;
    private readonly LogService _log = LogService.Instance;
    private readonly SystemInfoService _sysInfo = SystemInfoService.Instance;

    private SystemSecurityService() { }

    #region UAC

    private const string UacKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";

    /// <summary>检测 UAC 当前状态（true=开启）</summary>
    public bool IsUacEnabled()
    {
        int? value = _reg.GetDword(RegistryHive.LocalMachine, UacKeyPath, "EnableLUA");
        return value != 0;
    }

    /// <summary>关闭 UAC</summary>
    public bool DisableUac()
    {
        _log.Warning("[UAC] 正在关闭用户账户控制，需要重启生效");
        bool result = _reg.SetDword(RegistryHive.LocalMachine, UacKeyPath, "EnableLUA", 0);
        if (result)
            _log.Success("[UAC] 已关闭（重启后生效）");
        else
            _log.Error("[UAC] 关闭失败");
        return result;
    }

    /// <summary>恢复 UAC</summary>
    public bool EnableUac()
    {
        bool result = _reg.SetDword(RegistryHive.LocalMachine, UacKeyPath, "EnableLUA", 1);
        if (result)
            _log.Success("[UAC] 已恢复开启（重启后生效）");
        return result;
    }

    #endregion

    #region Defender

    private const string DefenderPolicyPath = @"SOFTWARE\Policies\Microsoft\Windows Defender";
    private const string DefenderRealtimePath = @"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection";
    private const string DefenderRegistryPath = @"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection";

    /// <summary>检测 Defender 实时防护状态</summary>
    public bool IsDefenderRealtimeEnabled()
    {
        // 优先检查策略路径
        int? policyValue = _reg.GetDword(RegistryHive.LocalMachine, DefenderRealtimePath, "DisableRealtimeMonitoring");
        if (policyValue == 1) return false;

        // 再检查直接注册表路径
        int? directValue = _reg.GetDword(RegistryHive.LocalMachine, DefenderRegistryPath, "DisableRealtimeMonitoring");
        if (directValue == 1) return false;

        return true;
    }

    /// <summary>
    /// 压制 Defender 实时防护
    /// 自动适配系统版本，新版 Win11 24H2+ 明确日志提示策略废弃
    /// </summary>
    public bool SuppressDefender()
    {
        var sysInfo = _sysInfo.Current;

        // 新版 Win11 策略废弃，必须明确提示用户
        if (sysInfo.IsNewWin11DefenderRestricted)
        {
            _log.DefenderPolicyDeprecated(sysInfo.BuildNumber, sysInfo.DisplayVersion);
            _log.Warning("[Defender] 尝试通过注册表方式压制，但系统可能会自动恢复此设置");
        }

        // Insider 预览版提示
        if (sysInfo.IsInsiderPreview)
        {
            _log.InsiderResetRisk("Defender");
        }

        // 家庭版提示
        if (!sysInfo.SupportsGroupPolicy)
        {
            _log.HomeEditionPolicyWarning("Defender");
        }

        bool success = true;

        // 方式1：通过策略路径（非家庭版）
        if (sysInfo.SupportsGroupPolicy)
        {
            success &= _reg.SetDword(RegistryHive.LocalMachine, DefenderPolicyPath, "DisableAntiSpyware", 1);
            success &= _reg.SetDword(RegistryHive.LocalMachine, DefenderRealtimePath, "DisableRealtimeMonitoring", 1);
            success &= _reg.SetDword(RegistryHive.LocalMachine, DefenderRealtimePath, "DisableBehaviorMonitoring", 1);
            success &= _reg.SetDword(RegistryHive.LocalMachine, DefenderRealtimePath, "DisableOnAccessProtection", 1);
            success &= _reg.SetDword(RegistryHive.LocalMachine, DefenderRealtimePath, "DisableScanOnRealtimeEnable", 1);
        }

        // 方式2：直接注册表（家庭版或作为补充）
        success &= _reg.SetDword(RegistryHive.LocalMachine, DefenderRegistryPath, "DisableRealtimeMonitoring", 1);

        if (success)
            _log.Success("[Defender] 实时防护已压制（可能需要重启生效）");
        else
            _log.Error("[Defender] 部分操作失败，请检查权限");

        return success;
    }

    /// <summary>恢复 Defender 实时防护</summary>
    public bool RestoreDefender()
    {
        var sysInfo = _sysInfo.Current;
        bool success = true;

        if (sysInfo.SupportsGroupPolicy)
        {
            success &= _reg.DeleteValue(RegistryHive.LocalMachine, DefenderPolicyPath, "DisableAntiSpyware");
            success &= _reg.DeleteValue(RegistryHive.LocalMachine, DefenderRealtimePath, "DisableRealtimeMonitoring");
            success &= _reg.DeleteValue(RegistryHive.LocalMachine, DefenderRealtimePath, "DisableBehaviorMonitoring");
            success &= _reg.DeleteValue(RegistryHive.LocalMachine, DefenderRealtimePath, "DisableOnAccessProtection");
            success &= _reg.DeleteValue(RegistryHive.LocalMachine, DefenderRealtimePath, "DisableScanOnRealtimeEnable");
        }

        success &= _reg.DeleteValue(RegistryHive.LocalMachine, DefenderRegistryPath, "DisableRealtimeMonitoring");

        if (success)
            _log.Success("[Defender] 实时防护策略已恢复");
        return success;
    }

    #endregion

    #region Firewall

    private const string FirewallDomainPath = @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\DomainProfile";
    private const string FirewallPrivatePath = @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile";
    private const string FirewallPublicPath = @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\PublicProfile";

    /// <summary>检测防火墙状态（任一配置文件开启即返回 true）</summary>
    public bool IsFirewallEnabled()
    {
        int? domain = _reg.GetDword(RegistryHive.LocalMachine, FirewallDomainPath, "EnableFirewall");
        int? priv = _reg.GetDword(RegistryHive.LocalMachine, FirewallPrivatePath, "EnableFirewall");
        int? pub = _reg.GetDword(RegistryHive.LocalMachine, FirewallPublicPath, "EnableFirewall");

        return (domain ?? 1) != 0 || (priv ?? 1) != 0 || (pub ?? 1) != 0;
    }

    /// <summary>完全关闭防火墙（域/专用/公用）</summary>
    public bool DisableFirewall()
    {
        _log.Warning("[防火墙] 正在关闭所有防火墙配置文件（域/专用/公用）");

        bool success = true;
        success &= _reg.SetDword(RegistryHive.LocalMachine, FirewallDomainPath, "EnableFirewall", 0);
        success &= _reg.SetDword(RegistryHive.LocalMachine, FirewallPrivatePath, "EnableFirewall", 0);
        success &= _reg.SetDword(RegistryHive.LocalMachine, FirewallPublicPath, "EnableFirewall", 0);

        if (success)
            _log.Success("[防火墙] 已完全关闭");
        else
            _log.Error("[防火墙] 部分配置文件关闭失败");
        return success;
    }

    /// <summary>恢复防火墙</summary>
    public bool EnableFirewall()
    {
        bool success = true;
        success &= _reg.SetDword(RegistryHive.LocalMachine, FirewallDomainPath, "EnableFirewall", 1);
        success &= _reg.SetDword(RegistryHive.LocalMachine, FirewallPrivatePath, "EnableFirewall", 1);
        success &= _reg.SetDword(RegistryHive.LocalMachine, FirewallPublicPath, "EnableFirewall", 1);

        if (success)
            _log.Success("[防火墙] 已全部恢复开启");
        return success;
    }

    #endregion
}
