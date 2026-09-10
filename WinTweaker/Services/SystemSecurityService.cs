using System.Management;
using Microsoft.Win32;
using WinTweaker.Models;

namespace WinTweaker.Services;

/// <summary>
/// 系统安全服务 —— UAC / Defender / 防火墙 / 内存完整性（HVCI）
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

    #region HVCI / Memory Integrity

    private const string DeviceGuardPath =
        @"SYSTEM\CurrentControlSet\Control\DeviceGuard";
    private const string HvciPath =
        @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity";

    /// <summary>内存完整性（HVCI）运行态：关闭 / 运行中 / 待重启。</summary>
    public enum MemoryIntegrityState
    {
        Off,
        OnRunning,
        PendingRestart
    }

    /// <summary>BIOS/固件是否已开启 CPU 硬件虚拟化（Intel VT-x / AMD-V）。</summary>
    public bool IsHardwareVirtualizationEnabled()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT VirtualizationFirmwareEnabled FROM Win32_Processor");
            using var results = searcher.Get();
            foreach (ManagementBaseObject obj in results)
            {
                using (obj as IDisposable)
                {
                    object? raw = obj["VirtualizationFirmwareEnabled"];
                    if (raw != null && Convert.ToBoolean(raw))
                        return true;
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            // 检测失败时不误伤可用机器：允许操作，由注册表写入结果报错
            _log.Warning($"[HVCI] 无法检测 CPU 硬件虚拟化状态：{ex.Message}");
            return true;
        }
    }

    /// <summary>注册表中的内存完整性期望开关（Enabled==1）。</summary>
    public bool IsMemoryIntegrityConfigured()
    {
        return _reg.GetDword(RegistryHive.LocalMachine, HvciPath, "Enabled") == 1;
    }

    /// <summary>
    /// 读取 DeviceGuard CIM 与注册表，判定三态：
    /// 运行中 / 已关闭 / 待重启生效。
    /// </summary>
    public MemoryIntegrityState GetMemoryIntegrityState()
    {
        bool registryOn = IsMemoryIntegrityConfigured();
        var snap = QueryDeviceGuard();

        if (snap is null)
            return registryOn ? MemoryIntegrityState.PendingRestart : MemoryIntegrityState.Off;

        bool running = snap.VbsStatus == 2 && ContainsService(snap.ServicesRunning, 2);
        bool configuredNotRunning = snap.VbsStatus == 1 && ContainsService(snap.ServicesConfigured, 2);

        // 注册表期望与实际运行不一致 → 待重启
        if (running != registryOn)
            return MemoryIntegrityState.PendingRestart;
        if (configuredNotRunning)
            return MemoryIntegrityState.PendingRestart;
        if (running)
            return MemoryIntegrityState.OnRunning;
        return MemoryIntegrityState.Off;
    }

    public string GetMemoryIntegrityStatusText()
    {
        return GetMemoryIntegrityState() switch
        {
            MemoryIntegrityState.OnRunning => "当前状态：已开启（运行中）",
            MemoryIntegrityState.PendingRestart => "当前状态：待重启生效（配置已更改但尚未重启）",
            _ => "当前状态：已关闭"
        };
    }

    /// <summary>开启内存完整性（写入注册表，需重启生效）。</summary>
    public bool EnableMemoryIntegrity() => SetMemoryIntegrity(enable: true);

    /// <summary>关闭内存完整性（写入注册表，需重启生效）。</summary>
    public bool DisableMemoryIntegrity() => SetMemoryIntegrity(enable: false);

    private bool SetMemoryIntegrity(bool enable)
    {
        int target = enable ? 1 : 0;
        string action = enable ? "开启" : "关闭";
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        int? beforeHvci = _reg.GetDword(RegistryHive.LocalMachine, HvciPath, "Enabled");
        int? beforeVbs = _reg.GetDword(RegistryHive.LocalMachine, DeviceGuardPath, "EnableVirtualizationBasedSecurity");

        _log.Warning(
            $"[HVCI] {timestamp} 准备{action}内存完整性 | 改前 Enabled={Fmt(beforeHvci)}, EnableVirtualizationBasedSecurity={Fmt(beforeVbs)}");

        bool ok = _reg.SetDword(RegistryHive.LocalMachine, HvciPath, "Enabled", target);
        ok &= _reg.SetDword(RegistryHive.LocalMachine, DeviceGuardPath, "EnableVirtualizationBasedSecurity", target);

        int? afterHvci = _reg.GetDword(RegistryHive.LocalMachine, HvciPath, "Enabled");
        int? afterVbs = _reg.GetDword(RegistryHive.LocalMachine, DeviceGuardPath, "EnableVirtualizationBasedSecurity");

        _log.Info(
            $"[HVCI] {timestamp} 改后 Enabled={Fmt(afterHvci)}, EnableVirtualizationBasedSecurity={Fmt(afterVbs)}");

        if (ok && afterHvci == target && afterVbs == target)
        {
            _log.Success($"[HVCI] 已{action}内存完整性（需重启电脑后生效）");
            return true;
        }

        _log.Error($"[HVCI] {action}失败：注册表写入未成功，请确认以管理员权限运行");
        return false;
    }

    private sealed class DeviceGuardSnapshot
    {
        public int VbsStatus { get; init; }
        public int[] ServicesRunning { get; init; } = [];
        public int[] ServicesConfigured { get; init; } = [];
    }

    private DeviceGuardSnapshot? QueryDeviceGuard()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\Microsoft\Windows\DeviceGuard",
                "SELECT VirtualizationBasedSecurityStatus, SecurityServicesRunning, SecurityServicesConfigured FROM Win32_DeviceGuard");

            using var results = searcher.Get();
            foreach (ManagementBaseObject obj in results)
            {
                using (obj as IDisposable)
                {
                    return new DeviceGuardSnapshot
                    {
                        VbsStatus = Convert.ToInt32(obj["VirtualizationBasedSecurityStatus"] ?? 0),
                        ServicesRunning = ToIntArray(obj["SecurityServicesRunning"]),
                        ServicesConfigured = ToIntArray(obj["SecurityServicesConfigured"])
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _log.Warning($"[HVCI] 读取 Win32_DeviceGuard 失败：{ex.Message}");
        }

        return null;
    }

    private static bool ContainsService(int[] services, int code)
        => Array.IndexOf(services, code) >= 0;

    private static int[] ToIntArray(object? value)
    {
        if (value is null) return [];
        if (value is int[] ints) return ints;
        if (value is uint[] uints)
        {
            var mapped = new int[uints.Length];
            for (int i = 0; i < uints.Length; i++)
                mapped[i] = (int)uints[i];
            return mapped;
        }
        if (value is Array arr)
        {
            var list = new List<int>(arr.Length);
            foreach (object? item in arr)
            {
                if (item != null)
                    list.Add(Convert.ToInt32(item));
            }
            return list.ToArray();
        }
        return [];
    }

    private static string Fmt(int? value) => value?.ToString() ?? "(未设置)";

    #endregion
}
