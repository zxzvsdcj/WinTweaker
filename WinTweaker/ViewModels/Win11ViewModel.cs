using Microsoft.Win32;
using WinTweaker.Models;
using WinTweaker.Services;

namespace WinTweaker.ViewModels;

/// <summary>
/// Win11 专属功能 ViewModel
/// 自动判断系统兼容性，不兼容时置灰并显示提示
/// </summary>
public sealed class Win11ViewModel : ViewModelBase
{
    private readonly RegistryService _reg = RegistryService.Instance;
    private readonly LogService _log = LogService.Instance;
    private readonly SystemCapabilities _caps;

    private bool _isCopilotDisabled;
    private bool _isWidgetsDisabled;
    private bool _isTaskbarBingDisabled;

    public bool CanDisableCopilot => _caps.CanDisableCopilot;
    public bool CanDisableWidgets => _caps.CanDisableWidgets;
    public bool CanDisableTaskbarBing => _caps.CanDisableTaskbarBing;

    /// <summary>Win10 用户看到的提示</summary>
    public bool ShowWin10Warning => !SystemInfoService.Instance.Current.IsWindows11;

    public string CopilotTooltip => CanDisableCopilot
        ? "关闭 Copilot 后台常驻进程"
        : "此功能仅适用于 Windows 11";

    public string WidgetsTooltip => CanDisableWidgets
        ? "禁用任务栏 Widgets 小组件服务"
        : "此功能仅适用于 Windows 11";

    public string TaskbarBingTooltip => CanDisableTaskbarBing
        ? "移除任务栏搜索框中的 Bing 网络搜索"
        : "此功能仅适用于 Windows 11";

    public bool IsCopilotDisabled
    {
        get => _isCopilotDisabled;
        set
        {
            if (!CanDisableCopilot) return;
            if (SetProperty(ref _isCopilotDisabled, value))
            {
                if (value) DisableCopilot();
                else RestoreCopilot();
            }
        }
    }

    public bool IsWidgetsDisabled
    {
        get => _isWidgetsDisabled;
        set
        {
            if (!CanDisableWidgets) return;
            if (SetProperty(ref _isWidgetsDisabled, value))
            {
                if (value) DisableWidgets();
                else RestoreWidgets();
            }
        }
    }

    public bool IsTaskbarBingDisabled
    {
        get => _isTaskbarBingDisabled;
        set
        {
            if (!CanDisableTaskbarBing) return;
            if (SetProperty(ref _isTaskbarBingDisabled, value))
            {
                if (value) DisableTaskbarBing();
                else RestoreTaskbarBing();
            }
        }
    }

    public Win11ViewModel()
    {
        _caps = new SystemCapabilities(SystemInfoService.Instance.Current);

        if (!CanDisableCopilot)
            _log.VersionNotSupported("Copilot", "仅 Windows 11 支持");

        ScanCurrentState();
    }

    private void ScanCurrentState()
    {
        if (!CanDisableCopilot) return;

        _isCopilotDisabled = _reg.GetDword(RegistryHive.CurrentUser,
            @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot") == 1;
        OnPropertyChanged(nameof(IsCopilotDisabled));

        _isWidgetsDisabled = _reg.GetDword(RegistryHive.LocalMachine,
            @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests") == 0;
        OnPropertyChanged(nameof(IsWidgetsDisabled));

        _isTaskbarBingDisabled = _reg.GetDword(RegistryHive.CurrentUser,
            @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions") == 1;
        OnPropertyChanged(nameof(IsTaskbarBingDisabled));
    }

    private void DisableCopilot()
    {
        _reg.SetDword(RegistryHive.CurrentUser,
            @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1);
        _log.Success("[Copilot] 已禁用（重启资源管理器或重启系统后生效）");
    }

    private void RestoreCopilot()
    {
        _reg.DeleteValue(RegistryHive.CurrentUser,
            @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot");
        _log.Success("[Copilot] 已恢复");
    }

    private void DisableWidgets()
    {
        _reg.SetDword(RegistryHive.LocalMachine,
            @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0);
        _log.Success("[Widgets] 小组件已禁用");
    }

    private void RestoreWidgets()
    {
        _reg.DeleteValue(RegistryHive.LocalMachine,
            @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests");
        _log.Success("[Widgets] 小组件已恢复");
    }

    private void DisableTaskbarBing()
    {
        _reg.SetDword(RegistryHive.CurrentUser,
            @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1);
        _reg.SetDword(RegistryHive.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0);
        _log.Success("[Bing搜索] 任务栏搜索已禁用网络建议");
    }

    private void RestoreTaskbarBing()
    {
        _reg.DeleteValue(RegistryHive.CurrentUser,
            @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions");
        _reg.DeleteValue(RegistryHive.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled");
        _log.Success("[Bing搜索] 已恢复网络搜索建议");
    }
}
