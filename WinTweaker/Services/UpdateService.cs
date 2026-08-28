using Microsoft.Win32;

namespace WinTweaker.Services;

/// <summary>
/// 更新管理服务 —— 处理 Windows Update / Edge / Chrome 自动更新的禁用与恢复
/// </summary>
public sealed class UpdateService
{
    private static readonly Lazy<UpdateService> _instance = new(() => new UpdateService());
    public static UpdateService Instance => _instance.Value;

    private readonly RegistryService _reg = RegistryService.Instance;
    private readonly ServiceManager _svc = ServiceManager.Instance;
    private readonly LogService _log = LogService.Instance;

    private UpdateService() { }

    #region Windows Update

    private static readonly string[] WindowsUpdateServices = ["wuauserv", "UsoSvc", "WaaSMedicSvc"];

    /// <summary>
    /// 检测 Windows Update 是否已被禁用
    /// 同时检查策略注册表和服务状态
    /// </summary>
    public bool IsWindowsUpdateDisabled()
    {
        var noAutoUpdate = _reg.GetDword(RegistryHive.LocalMachine,
            @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoUpdate");
        if (noAutoUpdate == 1) return true;

        foreach (var svc in WindowsUpdateServices)
        {
            if (!_svc.ServiceExists(svc)) continue;
            var startType = _svc.GetStartType(svc);
            if (startType != System.ServiceProcess.ServiceStartMode.Disabled)
                return false;
        }
        return true;
    }

    /// <summary>
    /// 禁用 Windows Update（三层防护：策略 + 服务禁用 + 停止运行）
    /// </summary>
    public void DisableWindowsUpdate()
    {
        // 策略层：设置 NoAutoUpdate
        _reg.SetDword(RegistryHive.LocalMachine,
            @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoUpdate", 1);
        _reg.SetDword(RegistryHive.LocalMachine,
            @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "AUOptions", 2);

        // 服务层：禁用并停止更新相关服务
        _svc.DisableServices(WindowsUpdateServices);

        _log.Success("[Windows Update] 已禁用自动更新（策略 + 服务）");
    }

    /// <summary>
    /// 恢复 Windows Update
    /// </summary>
    public void RestoreWindowsUpdate()
    {
        // 删除策略键值
        _reg.DeleteValue(RegistryHive.LocalMachine,
            @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoUpdate");
        _reg.DeleteValue(RegistryHive.LocalMachine,
            @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "AUOptions");

        // 恢复服务原始状态
        _svc.RestoreServices(WindowsUpdateServices);

        _log.Success("[Windows Update] 已恢复自动更新");
    }

    #endregion

    #region Edge Update

    private static readonly string[] EdgeUpdateServices = ["edgeupdate", "edgeupdatem"];

    private const string EdgeUpdatePolicyPath = @"SOFTWARE\Policies\Microsoft\EdgeUpdate";

    /// <summary>
    /// 检测 Edge 自动更新是否已被禁用
    /// </summary>
    public bool IsEdgeUpdateDisabled()
    {
        var updateDefault = _reg.GetDword(RegistryHive.LocalMachine,
            EdgeUpdatePolicyPath, "UpdateDefault");
        return updateDefault == 0;
    }

    /// <summary>
    /// 禁用 Edge 自动更新（注册表策略 + 服务）
    /// </summary>
    public void DisableEdgeUpdate()
    {
        // 策略层：禁用所有 Edge 通道更新
        _reg.SetDword(RegistryHive.LocalMachine, EdgeUpdatePolicyPath, "UpdateDefault", 0);
        _reg.SetDword(RegistryHive.LocalMachine, EdgeUpdatePolicyPath, "AutoUpdateCheckPeriodMinutes", 0);
        // Stable 通道 GUID
        _reg.SetDword(RegistryHive.LocalMachine, EdgeUpdatePolicyPath,
            "Update{56EB18F8-B008-4CBD-B6D2-8C97FE7E9062}", 0);

        // 服务层
        _svc.DisableServices(EdgeUpdateServices);

        _log.Success("[Edge Update] 已禁用自动更新");
    }

    /// <summary>
    /// 恢复 Edge 自动更新
    /// </summary>
    public void RestoreEdgeUpdate()
    {
        _reg.DeleteValue(RegistryHive.LocalMachine, EdgeUpdatePolicyPath, "UpdateDefault");
        _reg.DeleteValue(RegistryHive.LocalMachine, EdgeUpdatePolicyPath, "AutoUpdateCheckPeriodMinutes");
        _reg.DeleteValue(RegistryHive.LocalMachine, EdgeUpdatePolicyPath,
            "Update{56EB18F8-B008-4CBD-B6D2-8C97FE7E9062}");

        _svc.RestoreServices(EdgeUpdateServices);

        _log.Success("[Edge Update] 已恢复自动更新");
    }

    #endregion

    #region Chrome Update

    private static readonly string[] ChromeUpdateServices = ["gupdate", "gupdatem"];

    // Google Update 使用 32 位注册表视图
    private const string ChromeUpdatePolicyPath = @"SOFTWARE\Policies\Google\Update";
    private const string ChromeAppGuid = "Update{8A69D345-D564-463C-AFF1-A69D9E530F96}";

    /// <summary>
    /// 检测 Chrome 自动更新是否已被禁用
    /// Google Update 读取 32 位注册表视图
    /// </summary>
    public bool IsChromeUpdateDisabled()
    {
        var updateDefault = _reg.GetDword(RegistryHive.LocalMachine,
            ChromeUpdatePolicyPath, "UpdateDefault", RegistryView.Registry32);
        if (updateDefault == 0) return true;

        var appUpdate = _reg.GetDword(RegistryHive.LocalMachine,
            ChromeUpdatePolicyPath, ChromeAppGuid, RegistryView.Registry32);
        return appUpdate == 0;
    }

    /// <summary>
    /// 禁用 Chrome 自动更新（32位注册表 + 服务）
    /// </summary>
    public void DisableChromeUpdate()
    {
        // 策略层（32位视图，Google Update 专用）
        _reg.SetDword(RegistryHive.LocalMachine, ChromeUpdatePolicyPath,
            "UpdateDefault", 0, RegistryView.Registry32);
        _reg.SetDword(RegistryHive.LocalMachine, ChromeUpdatePolicyPath,
            "AutoUpdateCheckPeriodMinutes", 0, RegistryView.Registry32);
        _reg.SetDword(RegistryHive.LocalMachine, ChromeUpdatePolicyPath,
            ChromeAppGuid, 0, RegistryView.Registry32);

        // 同时写入 64 位视图确保覆盖
        _reg.SetDword(RegistryHive.LocalMachine, ChromeUpdatePolicyPath, "UpdateDefault", 0);
        _reg.SetDword(RegistryHive.LocalMachine, ChromeUpdatePolicyPath, "AutoUpdateCheckPeriodMinutes", 0);
        _reg.SetDword(RegistryHive.LocalMachine, ChromeUpdatePolicyPath, ChromeAppGuid, 0);

        // 服务层
        _svc.DisableServices(ChromeUpdateServices);

        _log.Success("[Chrome Update] 已禁用自动更新");
    }

    /// <summary>
    /// 恢复 Chrome 自动更新
    /// </summary>
    public void RestoreChromeUpdate()
    {
        // 清除 32 位视图策略
        _reg.DeleteValue(RegistryHive.LocalMachine, ChromeUpdatePolicyPath,
            "UpdateDefault", RegistryView.Registry32);
        _reg.DeleteValue(RegistryHive.LocalMachine, ChromeUpdatePolicyPath,
            "AutoUpdateCheckPeriodMinutes", RegistryView.Registry32);
        _reg.DeleteValue(RegistryHive.LocalMachine, ChromeUpdatePolicyPath,
            ChromeAppGuid, RegistryView.Registry32);

        // 清除 64 位视图策略
        _reg.DeleteValue(RegistryHive.LocalMachine, ChromeUpdatePolicyPath, "UpdateDefault");
        _reg.DeleteValue(RegistryHive.LocalMachine, ChromeUpdatePolicyPath, "AutoUpdateCheckPeriodMinutes");
        _reg.DeleteValue(RegistryHive.LocalMachine, ChromeUpdatePolicyPath, ChromeAppGuid);

        _svc.RestoreServices(ChromeUpdateServices);

        _log.Success("[Chrome Update] 已恢复自动更新");
    }

    #endregion
}
