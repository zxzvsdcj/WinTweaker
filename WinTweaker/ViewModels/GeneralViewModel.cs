using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WinTweaker.License;
using WinTweaker.Models;
using WinTweaker.Services;

namespace WinTweaker.ViewModels;

/// <summary>
/// 常规优化 ViewModel
/// </summary>
public sealed class GeneralViewModel : ViewModelBase
{
    private readonly RegistryService _reg = RegistryService.Instance;
    private readonly ServiceManager _svc = ServiceManager.Instance;
    private readonly LogService _log = LogService.Instance;
    private readonly SystemSecurityService _security = SystemSecurityService.Instance;

    private bool _isUltimatePowerEnabled;
    private bool _isTelemetryReduced;
    private bool _isAdsDisabled;
    private bool _isServicesOptimized;
    private bool _isBackgroundDisabled;
    private bool _isHibernationDisabled;
    private bool _isExplorerOptimized;
    private bool _isFileExtensionsShown;
    private bool _isReservedStorageDisabled;
    private bool _isMemoryIntegrityEnabled;
    private bool _canToggleMemoryIntegrity;
    private string _memoryIntegrityStatusText = string.Empty;
    private string _memoryIntegrityDescription = string.Empty;

    public bool IsUltimatePowerEnabled
    {
        get => _isUltimatePowerEnabled;
        set
        {
            if (SetProperty(ref _isUltimatePowerEnabled, value))
            {
                if (value) EnableUltimatePowerPlan();
                else DisableUltimatePowerPlan();
            }
        }
    }

    public bool IsTelemetryReduced
    {
        get => _isTelemetryReduced;
        set
        {
            if (SetProperty(ref _isTelemetryReduced, value))
            {
                if (value) ReduceTelemetry();
                else RestoreTelemetry();
            }
        }
    }

    public bool IsAdsDisabled
    {
        get => _isAdsDisabled;
        set
        {
            if (SetProperty(ref _isAdsDisabled, value))
            {
                if (value) DisableAds();
                else RestoreAds();
            }
        }
    }

    public bool IsServicesOptimized
    {
        get => _isServicesOptimized;
        set
        {
            if (SetProperty(ref _isServicesOptimized, value))
            {
                if (value) OptimizeServices();
                else RestoreServicesOriginal();
            }
        }
    }

    public bool IsBackgroundDisabled
    {
        get => _isBackgroundDisabled;
        set
        {
            if (SetProperty(ref _isBackgroundDisabled, value))
            {
                if (value) DisableBackground();
                else RestoreBackground();
            }
        }
    }

    public bool IsHibernationDisabled
    {
        get => _isHibernationDisabled;
        set
        {
            if (SetProperty(ref _isHibernationDisabled, value))
            {
                if (value) DisableHibernation();
                else EnableHibernation();
            }
        }
    }

    public bool IsExplorerOptimized
    {
        get => _isExplorerOptimized;
        set
        {
            if (SetProperty(ref _isExplorerOptimized, value))
            {
                if (value) OptimizeExplorer();
                else RestoreExplorer();
            }
        }
    }

    public bool IsFileExtensionsShown
    {
        get => _isFileExtensionsShown;
        set
        {
            if (SetProperty(ref _isFileExtensionsShown, value))
            {
                if (value) ShowFileExtensions();
                else HideFileExtensions();
            }
        }
    }

    public bool IsReservedStorageDisabled
    {
        get => _isReservedStorageDisabled;
        set
        {
            if (SetProperty(ref _isReservedStorageDisabled, value))
            {
                if (value) DisableReservedStorage();
                else EnableReservedStorage();
            }
        }
    }

    /// <summary>内存完整性（HVCI）开关：绑定注册表期望状态。</summary>
    public bool IsMemoryIntegrityEnabled
    {
        get => _isMemoryIntegrityEnabled;
        set
        {
            if (!_canToggleMemoryIntegrity)
            {
                SetProperty(ref _isMemoryIntegrityEnabled, _security.IsMemoryIntegrityConfigured());
                return;
            }

            if (!SetProperty(ref _isMemoryIntegrityEnabled, value))
                return;

            if (value)
            {
                if (!ConfirmMemoryIntegrityChange(enable: true))
                {
                    SetProperty(ref _isMemoryIntegrityEnabled, false);
                    return;
                }

                if (!_security.EnableMemoryIntegrity())
                {
                    SetProperty(ref _isMemoryIntegrityEnabled, false);
                    MessageBox.Show(
                        "开启内存完整性失败：注册表写入未成功。请确认以管理员权限运行本程序。",
                        "操作失败",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    RefreshMemoryIntegrityUi();
                    return;
                }
            }
            else
            {
                if (!ConfirmMemoryIntegrityChange(enable: false))
                {
                    SetProperty(ref _isMemoryIntegrityEnabled, true);
                    return;
                }

                if (!_security.DisableMemoryIntegrity())
                {
                    SetProperty(ref _isMemoryIntegrityEnabled, true);
                    MessageBox.Show(
                        "关闭内存完整性失败：注册表写入未成功。请确认以管理员权限运行本程序。",
                        "操作失败",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    RefreshMemoryIntegrityUi();
                    return;
                }
            }

            MessageBox.Show(
                "设置已更改，重启电脑后生效。",
                "内存完整性",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            RefreshMemoryIntegrityUi();
        }
    }

    public bool CanToggleMemoryIntegrity
    {
        get => _canToggleMemoryIntegrity;
        private set => SetProperty(ref _canToggleMemoryIntegrity, value);
    }

    public string MemoryIntegrityStatusText
    {
        get => _memoryIntegrityStatusText;
        private set => SetProperty(ref _memoryIntegrityStatusText, value);
    }

    public string MemoryIntegrityDescription
    {
        get => _memoryIntegrityDescription;
        private set => SetProperty(ref _memoryIntegrityDescription, value);
    }

    public ICommand GenerateWslConfigCommand { get; }

    public GeneralViewModel()
    {
        GenerateWslConfigCommand = new RelayCommand(GenerateWslConfig);
        ScanCurrentState();
    }

    /// <summary>开机自动扫描当前状态，从注册表/服务实际状态反推所有开关</summary>
    private void ScanCurrentState()
    {
        // 电源计划：检查注册表标记
        _isUltimatePowerEnabled = _reg.GetDword(RegistryHive.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings",
            "UltimatePerformance") == 1;
        OnPropertyChanged(nameof(IsUltimatePowerEnabled));

        // 遥测
        _isTelemetryReduced = _reg.GetDword(RegistryHive.LocalMachine,
            @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry") == 0;
        OnPropertyChanged(nameof(IsTelemetryReduced));

        // 广告：检测关键标志位（SilentInstalledAppsEnabled=0 代表已优化）
        _isAdsDisabled = _reg.GetDword(RegistryHive.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
            "SilentInstalledAppsEnabled") == 0;
        OnPropertyChanged(nameof(IsAdsDisabled));

        // 冗余服务：所有目标服务均为 Disabled 视为已优化
        _isServicesOptimized = CheckAllServicesDisabled(RedundantServices);
        OnPropertyChanged(nameof(IsServicesOptimized));

        // 后台运行
        _isBackgroundDisabled = _reg.GetDword(RegistryHive.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled") == 1;
        OnPropertyChanged(nameof(IsBackgroundDisabled));

        // 休眠
        _isHibernationDisabled = _reg.GetDword(RegistryHive.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled") == 0;
        OnPropertyChanged(nameof(IsHibernationDisabled));

        // 资源管理器：云广告关闭 + 显示受保护系统文件
        string advanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        _isExplorerOptimized = _reg.GetDword(RegistryHive.CurrentUser, advanced, "ShowSyncProviderNotifications") == 0
            && _reg.GetDword(RegistryHive.CurrentUser, advanced, "ShowSuperHidden") == 1;
        OnPropertyChanged(nameof(IsExplorerOptimized));

        // 始终显示文件扩展名
        _isFileExtensionsShown = _reg.GetDword(RegistryHive.CurrentUser, advanced, "HideFileExt") == 0;
        OnPropertyChanged(nameof(IsFileExtensionsShown));

        // 保留存储
        _isReservedStorageDisabled = _reg.GetDword(RegistryHive.LocalMachine,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\ReserveManager", "ShippedWithReserves") == 0;
        OnPropertyChanged(nameof(IsReservedStorageDisabled));

        // 内存完整性（HVCI）
        RefreshMemoryIntegrityUi();
    }

    private void RefreshMemoryIntegrityUi()
    {
        CanToggleMemoryIntegrity = _security.IsHardwareVirtualizationEnabled();
        _isMemoryIntegrityEnabled = _security.IsMemoryIntegrityConfigured();
        OnPropertyChanged(nameof(IsMemoryIntegrityEnabled));
        MemoryIntegrityStatusText = _security.GetMemoryIntegrityStatusText();

        if (!CanToggleMemoryIntegrity)
        {
            MemoryIntegrityDescription =
                "CPU 硬件虚拟化未启用，请在 BIOS 中开启 Intel VT-x / AMD-V。\n" +
                "内存完整性利用 Hypervisor 保护内核；关闭后便于 VMware/VirtualBox 使用虚拟化，但会降低内核防护。";
        }
        else
        {
            MemoryIntegrityDescription =
                "利用 Hypervisor 保护内核代码（HVCI）。关闭后可释放 VT-x/AMD-V 给 VMware/VirtualBox 等虚拟机软件，需重启生效。\n" +
                MemoryIntegrityStatusText;
        }
    }

    private static bool ConfirmMemoryIntegrityChange(bool enable)
    {
        if (ProfileApplyContext.SuppressDangerConfirm)
            return true;

        if (enable)
        {
            var result = MessageBox.Show(
                "开启后将独占 CPU 虚拟化资源，可能导致 VMware/VirtualBox 等虚拟机软件无法正常启动，需要重启生效。\n\n" +
                "点击「是」确认开启，点击「否」取消。",
                "确认开启内存完整性",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            return result == MessageBoxResult.Yes;
        }
        else
        {
            var result = MessageBox.Show(
                "关闭后将降低系统内核防护能力，且需要重启生效。如果你正在使用 VMware/VirtualBox 等虚拟机软件，建议关闭。\n\n" +
                "点击「是」确认关闭，点击「否」取消。",
                "确认关闭内存完整性",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            return result == MessageBoxResult.Yes;
        }
    }

    /// <summary>检查指定服务是否全部处于 Disabled 状态</summary>
    private bool CheckAllServicesDisabled(string[] serviceNames)
    {
        foreach (var name in serviceNames)
        {
            if (!_svc.ServiceExists(name))
                continue;
            var startType = _svc.GetStartType(name);
            if (startType != System.ServiceProcess.ServiceStartMode.Disabled)
                return false;
        }
        return true;
    }

    #region 电源计划

    private void EnableUltimatePowerPlan()
    {
        // 通过注册表启用卓越性能电源计划
        // GUID: e9a42b02-d5df-448d-aa00-03f14749eb61
        _reg.SetDword(RegistryHive.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings",
            "UltimatePerformance", 1);
        _log.Success("[电源计划] 已启用卓越性能模式");
    }

    private void DisableUltimatePowerPlan()
    {
        _reg.DeleteValue(RegistryHive.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings",
            "UltimatePerformance");
        _log.Success("[电源计划] 已恢复默认");
    }

    #endregion

    #region 遥测

    private void ReduceTelemetry()
    {
        _reg.SetDword(RegistryHive.LocalMachine,
            @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0);
        _reg.SetDword(RegistryHive.LocalMachine,
            @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "MaxTelemetryAllowed", 1);
        _svc.DisableService("DiagTrack");
        _log.Success("[遥测] 已降级为基础级别");
    }

    private void RestoreTelemetry()
    {
        _reg.DeleteValue(RegistryHive.LocalMachine,
            @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry");
        _reg.DeleteValue(RegistryHive.LocalMachine,
            @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "MaxTelemetryAllowed");
        _svc.RestoreService("DiagTrack");
        _log.Success("[遥测] 已恢复默认");
    }

    #endregion

    #region 广告

    private void DisableAds()
    {
        string cuContentDelivery = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
        _reg.SetDword(RegistryHive.CurrentUser, cuContentDelivery, "SilentInstalledAppsEnabled", 0);
        _reg.SetDword(RegistryHive.CurrentUser, cuContentDelivery, "SystemPaneSuggestionsEnabled", 0);
        _reg.SetDword(RegistryHive.CurrentUser, cuContentDelivery, "SoftLandingEnabled", 0);
        _reg.SetDword(RegistryHive.CurrentUser, cuContentDelivery, "SubscribedContent-338389Enabled", 0);
        _reg.SetDword(RegistryHive.CurrentUser, cuContentDelivery, "SubscribedContent-310093Enabled", 0);
        _reg.SetDword(RegistryHive.CurrentUser, cuContentDelivery, "SubscribedContent-338388Enabled", 0);
        _reg.SetDword(RegistryHive.CurrentUser, cuContentDelivery, "RotatingLockScreenOverlayEnabled", 0);

        _reg.SetDword(RegistryHive.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSyncProviderNotifications", 0);

        _log.Success("[广告] 已关闭系统全部广告和推荐");
    }

    private void RestoreAds()
    {
        string cuContentDelivery = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
        _reg.SetDword(RegistryHive.CurrentUser, cuContentDelivery, "SilentInstalledAppsEnabled", 1);
        _reg.SetDword(RegistryHive.CurrentUser, cuContentDelivery, "SystemPaneSuggestionsEnabled", 1);
        _reg.SetDword(RegistryHive.CurrentUser, cuContentDelivery, "SoftLandingEnabled", 1);
        _reg.SetDword(RegistryHive.CurrentUser, cuContentDelivery, "SubscribedContent-338389Enabled", 1);
        _reg.SetDword(RegistryHive.CurrentUser, cuContentDelivery, "SubscribedContent-310093Enabled", 1);
        _reg.SetDword(RegistryHive.CurrentUser, cuContentDelivery, "SubscribedContent-338388Enabled", 1);
        _reg.SetDword(RegistryHive.CurrentUser, cuContentDelivery, "RotatingLockScreenOverlayEnabled", 1);

        _reg.SetDword(RegistryHive.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSyncProviderNotifications", 1);

        _log.Success("[广告] 已恢复默认");
    }

    #endregion

    #region 冗余服务

    private static readonly string[] RedundantServices =
    [
        "DiagTrack",
        "XblAuthManager", "XblGameSave", "XboxGipSvc", "XboxNetApiSvc",
        "SysMain"
    ];

    private void OptimizeServices()
    {
        _svc.DisableServices(RedundantServices);
        _log.Success("[服务优化] 已禁用冗余服务");
    }

    private void RestoreServicesOriginal()
    {
        _svc.RestoreServices(RedundantServices);
        _log.Success("[服务优化] 已恢复所有服务原始状态");
    }

    #endregion

    #region 后台运行

    private void DisableBackground()
    {
        _reg.SetDword(RegistryHive.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1);
        _log.Success("[后台] 已全局禁止应用后台运行");
    }

    private void RestoreBackground()
    {
        _reg.SetDword(RegistryHive.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 0);
        _log.Success("[后台] 已恢复应用后台运行权限");
    }

    #endregion

    #region 休眠

    private void DisableHibernation()
    {
        _reg.SetDword(RegistryHive.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled", 0);
        _log.Success("[休眠] 已关闭休眠（重启后释放 hiberfil.sys 空间）");
    }

    private void EnableHibernation()
    {
        _reg.SetDword(RegistryHive.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled", 1);
        _log.Success("[休眠] 已恢复休眠功能");
    }

    #endregion

    #region 资源管理器

    private void OptimizeExplorer()
    {
        string advanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        _reg.SetDword(RegistryHive.CurrentUser, advanced, "ShowSuperHidden", 1);
        _reg.SetDword(RegistryHive.CurrentUser, advanced, "ShowSyncProviderNotifications", 0);
        _log.Success("[资源管理器] 已优化：显示受保护系统文件、关闭云广告");
    }

    private void RestoreExplorer()
    {
        string advanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        _reg.SetDword(RegistryHive.CurrentUser, advanced, "ShowSuperHidden", 0);
        _reg.SetDword(RegistryHive.CurrentUser, advanced, "ShowSyncProviderNotifications", 1);
        _log.Success("[资源管理器] 已恢复默认");
    }

    private void ShowFileExtensions()
    {
        _reg.SetDword(RegistryHive.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 0);
        _log.Success("[文件扩展名] 已强制显示所有文件扩展名");
    }

    private void HideFileExtensions()
    {
        _reg.SetDword(RegistryHive.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 1);
        _log.Success("[文件扩展名] 已恢复隐藏已知扩展名");
    }

    private void DisableReservedStorage()
    {
        _reg.SetDword(RegistryHive.LocalMachine,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\ReserveManager", "ShippedWithReserves", 0);
        _log.Success("[保留存储] 已禁用（重启后逐步释放约数 GB；重大更新前建议重新启用）");
    }

    private void EnableReservedStorage()
    {
        _reg.SetDword(RegistryHive.LocalMachine,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\ReserveManager", "ShippedWithReserves", 1);
        _log.Success("[保留存储] 已恢复启用");
    }

    #endregion

    #region WSL2 配置

    private void GenerateWslConfig()
    {
        if (!LicenseGate.EnsureLicensed(out var deny))
        {
            _log.Error($"[授权] 已阻断 WSL 配置：{deny}");
            return;
        }
        try
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string wslConfigPath = Path.Combine(userProfile, ".wslconfig");

            int totalMemoryMb = (int)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024);
            int wslMemoryMb = Math.Max(4096, totalMemoryMb / 2);
            int processors = Math.Max(2, Environment.ProcessorCount / 2);

            string config = $"""
                [wsl2]
                memory={wslMemoryMb}MB
                processors={processors}
                swap=0
                localhostForwarding=true
                nestedVirtualization=true
                """;

            File.WriteAllText(wslConfigPath, config);
            _log.Success($"[WSL2] 配置已生成：{wslConfigPath}（内存 {wslMemoryMb}MB，{processors} 核）");
        }
        catch (Exception ex)
        {
            _log.Error($"[WSL2] 配置生成失败：{ex.Message}");
        }
    }

    #endregion
}
