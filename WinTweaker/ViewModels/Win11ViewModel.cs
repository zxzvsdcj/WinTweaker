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
    private const string ClassicMenuKey =
        @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";
    private const string ClassicMenuInprocKey =
        @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32";
    private const string GalleryKey =
        @"Software\Classes\CLSID\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}";
    private const string HomeKey =
        @"Software\Classes\CLSID\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}";
    private const string ExplorerAdvancedKey =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string TaskbarDeveloperKey =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings";
    private const string SearchHighlightsPolicyKey =
        @"Software\Policies\Microsoft\Windows\Windows Search";
    private const string OneDriveKey =
        @"Software\Classes\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}";
    private const string ContentDeliveryKey =
        @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
    private const string CabinetStateKey =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\CabinetState";
    private const string FileSystemKey =
        @"SYSTEM\CurrentControlSet\Control\FileSystem";

    private readonly RegistryService _reg = RegistryService.Instance;
    private readonly ExplorerService _explorer = ExplorerService.Instance;
    private readonly LogService _log = LogService.Instance;
    private readonly SystemCapabilities _caps;

    private bool _isCopilotDisabled;
    private bool _isWidgetsDisabled;
    private bool _isTaskbarBingDisabled;
    private bool _isClassicContextMenuEnabled;
    private bool _isTaskbarLeftAligned;
    private bool _isTaskbarEndTaskEnabled;
    private bool _isExplorerGalleryHidden;
    private bool _isSnapFlyoutDisabled;
    private bool _isExplorerHomeHidden;
    private bool _isStartRecommendationsDisabled;
    private bool _isFolderContentsInfoTipEnabled;
    private bool _isSearchHighlightsDisabled;
    private bool _isTaskbarSecondsEnabled;
    private bool _isOneDriveNavHidden;
    private bool _isLockScreenSpotlightDisabled;
    private bool _isFullPathInTitleEnabled;
    private bool _isLongPathsEnabled;

    public bool CanDisableCopilot => _caps.CanDisableCopilot;
    public bool CanDisableWidgets => _caps.CanDisableWidgets;
    public bool CanDisableTaskbarBing => _caps.CanDisableTaskbarBing;
    public bool CanClassicContextMenu => _caps.CanClassicContextMenu;
    public bool CanTaskbarLeftAlign => _caps.CanTaskbarLeftAlign;
    public bool CanTaskbarEndTask => _caps.CanTaskbarEndTask;
    public bool CanHideExplorerGallery => _caps.CanHideExplorerGallery;
    public bool CanDisableSnapFlyout => _caps.CanDisableSnapFlyout;
    public bool CanHideExplorerHome => _caps.CanHideExplorerHome;
    public bool CanDisableStartRecommendations => _caps.CanDisableStartRecommendations;
    public bool CanFolderContentsInfoTip => _caps.CanFolderContentsInfoTip;
    public bool CanDisableSearchHighlights => _caps.CanDisableSearchHighlights;
    public bool CanShowTaskbarSeconds => _caps.CanShowTaskbarSeconds;
    public bool CanHideOneDriveNav => _caps.CanHideOneDriveNav;
    public bool CanDisableLockScreenSpotlight => _caps.CanDisableLockScreenSpotlight;
    public bool CanShowFullPathInTitle => _caps.CanShowFullPathInTitle;
    public bool CanEnableLongPaths => _caps.CanEnableLongPaths;

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

    public string ClassicContextMenuTooltip => CanClassicContextMenu
        ? "恢复 Win10 风格完整右键菜单，免去「显示更多选项」"
        : "此功能仅适用于 Windows 11";

    public string TaskbarLeftAlignTooltip => CanTaskbarLeftAlign
        ? "将任务栏图标对齐到左侧（经典布局）"
        : "此功能仅适用于 Windows 11";

    public string TaskbarEndTaskTooltip => CanTaskbarEndTask
        ? "在任务栏图标右键菜单中启用「结束任务」"
        : _caps.GetUnavailableReason("TaskbarEndTask");

    public string HideExplorerGalleryTooltip => CanHideExplorerGallery
        ? "从资源管理器导航栏隐藏 Gallery（图库）入口"
        : "此功能仅适用于 Windows 11";

    public string DisableSnapFlyoutTooltip => CanDisableSnapFlyout
        ? "关闭最大化按钮悬停时的 Snap Layouts 布局提示（保留拖拽吸附）"
        : "此功能仅适用于 Windows 11";

    public string HideExplorerHomeTooltip => CanHideExplorerHome
        ? "从资源管理器导航栏隐藏 Home（主页）入口"
        : "此功能仅适用于 Windows 11";

    public string DisableStartRecommendationsTooltip => CanDisableStartRecommendations
        ? "关闭开始菜单「推荐」区域中的应用/技巧广告"
        : "此功能仅适用于 Windows 11";

    public string FolderContentsInfoTipTooltip => CanFolderContentsInfoTip
        ? "鼠标悬停文件夹时显示大小与内容摘要"
        : "此功能仅适用于 Windows 11";

    public string DisableSearchHighlightsTooltip => CanDisableSearchHighlights
        ? "关闭任务栏搜索框中的搜索高亮动态内容"
        : "此功能仅适用于 Windows 11";

    public string ShowTaskbarSecondsTooltip => CanShowTaskbarSeconds
        ? "任务栏时钟显示秒数"
        : "此功能仅适用于 Windows 11";

    public string HideOneDriveNavTooltip => CanHideOneDriveNav
        ? "从资源管理器导航栏隐藏 OneDrive（不卸载、不停同步）"
        : "此功能仅适用于 Windows 11";

    public string DisableLockScreenSpotlightTooltip => CanDisableLockScreenSpotlight
        ? "关闭锁屏 Windows Spotlight 壁纸与趣味提示"
        : "此功能仅适用于 Windows 11";

    public string ShowFullPathInTitleTooltip => CanShowFullPathInTitle
        ? "资源管理器标题栏显示完整文件夹路径"
        : _caps.GetUnavailableReason("ShowFullPathInTitle");

    public string EnableLongPathsTooltip => CanEnableLongPaths
        ? "启用 Win32 长路径（突破 MAX_PATH 260 限制，利于开发工具链）"
        : _caps.GetUnavailableReason("EnableLongPaths");

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

    public bool IsClassicContextMenuEnabled
    {
        get => _isClassicContextMenuEnabled;
        set
        {
            if (!CanClassicContextMenu) return;
            if (SetProperty(ref _isClassicContextMenuEnabled, value))
            {
                if (value) EnableClassicContextMenu();
                else RestoreModernContextMenu();
            }
        }
    }

    public bool IsTaskbarLeftAligned
    {
        get => _isTaskbarLeftAligned;
        set
        {
            if (!CanTaskbarLeftAlign) return;
            if (SetProperty(ref _isTaskbarLeftAligned, value))
            {
                if (value) AlignTaskbarLeft();
                else AlignTaskbarCenter();
            }
        }
    }

    public bool IsTaskbarEndTaskEnabled
    {
        get => _isTaskbarEndTaskEnabled;
        set
        {
            if (!CanTaskbarEndTask) return;
            if (SetProperty(ref _isTaskbarEndTaskEnabled, value))
            {
                if (value) EnableTaskbarEndTask();
                else DisableTaskbarEndTask();
            }
        }
    }

    public bool IsExplorerGalleryHidden
    {
        get => _isExplorerGalleryHidden;
        set
        {
            if (!CanHideExplorerGallery) return;
            if (SetProperty(ref _isExplorerGalleryHidden, value))
            {
                if (value) HideExplorerGallery();
                else ShowExplorerGallery();
            }
        }
    }

    public bool IsSnapFlyoutDisabled
    {
        get => _isSnapFlyoutDisabled;
        set
        {
            if (!CanDisableSnapFlyout) return;
            if (SetProperty(ref _isSnapFlyoutDisabled, value))
            {
                if (value) DisableSnapFlyout();
                else EnableSnapFlyout();
            }
        }
    }

    public bool IsExplorerHomeHidden
    {
        get => _isExplorerHomeHidden;
        set
        {
            if (!CanHideExplorerHome) return;
            if (SetProperty(ref _isExplorerHomeHidden, value))
            {
                if (value) HideExplorerHome();
                else ShowExplorerHome();
            }
        }
    }

    public bool IsStartRecommendationsDisabled
    {
        get => _isStartRecommendationsDisabled;
        set
        {
            if (!CanDisableStartRecommendations) return;
            if (SetProperty(ref _isStartRecommendationsDisabled, value))
            {
                if (value) DisableStartRecommendations();
                else EnableStartRecommendations();
            }
        }
    }

    public bool IsFolderContentsInfoTipEnabled
    {
        get => _isFolderContentsInfoTipEnabled;
        set
        {
            if (!CanFolderContentsInfoTip) return;
            if (SetProperty(ref _isFolderContentsInfoTipEnabled, value))
            {
                if (value) EnableFolderContentsInfoTip();
                else DisableFolderContentsInfoTip();
            }
        }
    }

    public bool IsSearchHighlightsDisabled
    {
        get => _isSearchHighlightsDisabled;
        set
        {
            if (!CanDisableSearchHighlights) return;
            if (SetProperty(ref _isSearchHighlightsDisabled, value))
            {
                if (value) DisableSearchHighlights();
                else EnableSearchHighlights();
            }
        }
    }

    public bool IsTaskbarSecondsEnabled
    {
        get => _isTaskbarSecondsEnabled;
        set
        {
            if (!CanShowTaskbarSeconds) return;
            if (SetProperty(ref _isTaskbarSecondsEnabled, value))
            {
                if (value) EnableTaskbarSeconds();
                else DisableTaskbarSeconds();
            }
        }
    }

    public bool IsOneDriveNavHidden
    {
        get => _isOneDriveNavHidden;
        set
        {
            if (!CanHideOneDriveNav) return;
            if (SetProperty(ref _isOneDriveNavHidden, value))
            {
                if (value) HideOneDriveNav();
                else ShowOneDriveNav();
            }
        }
    }

    public bool IsLockScreenSpotlightDisabled
    {
        get => _isLockScreenSpotlightDisabled;
        set
        {
            if (!CanDisableLockScreenSpotlight) return;
            if (SetProperty(ref _isLockScreenSpotlightDisabled, value))
            {
                if (value) DisableLockScreenSpotlight();
                else EnableLockScreenSpotlight();
            }
        }
    }

    public bool IsFullPathInTitleEnabled
    {
        get => _isFullPathInTitleEnabled;
        set
        {
            if (!CanShowFullPathInTitle) return;
            if (SetProperty(ref _isFullPathInTitleEnabled, value))
            {
                if (value) EnableFullPathInTitle();
                else DisableFullPathInTitle();
            }
        }
    }

    public bool IsLongPathsEnabled
    {
        get => _isLongPathsEnabled;
        set
        {
            if (!CanEnableLongPaths) return;
            if (SetProperty(ref _isLongPathsEnabled, value))
            {
                if (value) EnableLongPaths();
                else DisableLongPaths();
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
        if (CanDisableCopilot)
        {
            _isCopilotDisabled = _reg.GetDword(RegistryHive.CurrentUser,
                @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot") == 1;
            OnPropertyChanged(nameof(IsCopilotDisabled));
        }

        if (CanDisableWidgets)
        {
            _isWidgetsDisabled = _reg.GetDword(RegistryHive.LocalMachine,
                @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests") == 0;
            OnPropertyChanged(nameof(IsWidgetsDisabled));
        }

        if (CanDisableTaskbarBing)
        {
            _isTaskbarBingDisabled = _reg.GetDword(RegistryHive.CurrentUser,
                @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions") == 1;
            OnPropertyChanged(nameof(IsTaskbarBingDisabled));
        }

        if (CanClassicContextMenu)
        {
            _isClassicContextMenuEnabled = _reg.KeyExists(RegistryHive.CurrentUser, ClassicMenuInprocKey);
            OnPropertyChanged(nameof(IsClassicContextMenuEnabled));
        }

        if (CanTaskbarLeftAlign)
        {
            _isTaskbarLeftAligned = _reg.GetDword(RegistryHive.CurrentUser, ExplorerAdvancedKey, "TaskbarAl") == 0;
            OnPropertyChanged(nameof(IsTaskbarLeftAligned));
        }

        if (CanTaskbarEndTask)
        {
            _isTaskbarEndTaskEnabled = _reg.GetDword(RegistryHive.CurrentUser, TaskbarDeveloperKey, "TaskbarEndTask") == 1;
            OnPropertyChanged(nameof(IsTaskbarEndTaskEnabled));
        }

        if (CanHideExplorerGallery)
        {
            _isExplorerGalleryHidden = _reg.GetDword(RegistryHive.CurrentUser, GalleryKey, "System.IsPinnedToNameSpaceTree") == 0;
            OnPropertyChanged(nameof(IsExplorerGalleryHidden));
        }

        if (CanDisableSnapFlyout)
        {
            _isSnapFlyoutDisabled = _reg.GetDword(RegistryHive.CurrentUser, ExplorerAdvancedKey, "EnableSnapAssistFlyout") == 0;
            OnPropertyChanged(nameof(IsSnapFlyoutDisabled));
        }

        if (CanHideExplorerHome)
        {
            _isExplorerHomeHidden = _reg.GetDword(RegistryHive.CurrentUser, HomeKey, "System.IsPinnedToNameSpaceTree") == 0;
            OnPropertyChanged(nameof(IsExplorerHomeHidden));
        }

        if (CanDisableStartRecommendations)
        {
            _isStartRecommendationsDisabled = _reg.GetDword(RegistryHive.CurrentUser, ExplorerAdvancedKey, "Start_IrisRecommendations") == 0;
            OnPropertyChanged(nameof(IsStartRecommendationsDisabled));
        }

        if (CanFolderContentsInfoTip)
        {
            _isFolderContentsInfoTipEnabled = _reg.GetDword(RegistryHive.CurrentUser, ExplorerAdvancedKey, "FolderContentsInfoTip") == 1;
            OnPropertyChanged(nameof(IsFolderContentsInfoTipEnabled));
        }

        if (CanDisableSearchHighlights)
        {
            _isSearchHighlightsDisabled = _reg.GetDword(RegistryHive.CurrentUser, SearchHighlightsPolicyKey, "EnableDynamicContentInWSB") == 0;
            OnPropertyChanged(nameof(IsSearchHighlightsDisabled));
        }

        if (CanShowTaskbarSeconds)
        {
            _isTaskbarSecondsEnabled = _reg.GetDword(RegistryHive.CurrentUser, ExplorerAdvancedKey, "ShowSecondsInSystemClock") == 1;
            OnPropertyChanged(nameof(IsTaskbarSecondsEnabled));
        }

        if (CanHideOneDriveNav)
        {
            _isOneDriveNavHidden = _reg.GetDword(RegistryHive.CurrentUser, OneDriveKey, "System.IsPinnedToNameSpaceTree") == 0;
            OnPropertyChanged(nameof(IsOneDriveNavHidden));
        }

        if (CanDisableLockScreenSpotlight)
        {
            _isLockScreenSpotlightDisabled = _reg.GetDword(RegistryHive.CurrentUser, ContentDeliveryKey, "RotatingLockScreenEnabled") == 0;
            OnPropertyChanged(nameof(IsLockScreenSpotlightDisabled));
        }

        if (CanShowFullPathInTitle)
        {
            _isFullPathInTitleEnabled = _reg.GetDword(RegistryHive.CurrentUser, CabinetStateKey, "FullPath") == 1;
            OnPropertyChanged(nameof(IsFullPathInTitleEnabled));
        }

        if (CanEnableLongPaths)
        {
            _isLongPathsEnabled = _reg.GetDword(RegistryHive.LocalMachine, FileSystemKey, "LongPathsEnabled") == 1;
            OnPropertyChanged(nameof(IsLongPathsEnabled));
        }
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

    private void EnableClassicContextMenu()
    {
        if (_reg.SetEmptyDefault(RegistryHive.CurrentUser, ClassicMenuInprocKey))
        {
            _log.Success("[经典右键] 已启用经典完整菜单");
            _explorer.Restart();
        }
    }

    private void RestoreModernContextMenu()
    {
        if (_reg.DeleteSubKeyTree(RegistryHive.CurrentUser, ClassicMenuKey))
        {
            _log.Success("[经典右键] 已恢复 Win11 新版菜单");
            _explorer.Restart();
        }
    }

    private void AlignTaskbarLeft()
    {
        _reg.SetDword(RegistryHive.CurrentUser, ExplorerAdvancedKey, "TaskbarAl", 0);
        _log.Success("[任务栏] 已设置为左对齐");
        _explorer.Restart();
    }

    private void AlignTaskbarCenter()
    {
        _reg.SetDword(RegistryHive.CurrentUser, ExplorerAdvancedKey, "TaskbarAl", 1);
        _log.Success("[任务栏] 已恢复居中对齐");
        _explorer.Restart();
    }

    private void EnableTaskbarEndTask()
    {
        _reg.SetDword(RegistryHive.CurrentUser, TaskbarDeveloperKey, "TaskbarEndTask", 1);
        _log.Success("[结束任务] 任务栏右键「结束任务」已启用");
    }

    private void DisableTaskbarEndTask()
    {
        _reg.SetDword(RegistryHive.CurrentUser, TaskbarDeveloperKey, "TaskbarEndTask", 0);
        _log.Success("[结束任务] 任务栏右键「结束任务」已关闭");
    }

    private void HideExplorerGallery()
    {
        _reg.SetDword(RegistryHive.CurrentUser, GalleryKey, "System.IsPinnedToNameSpaceTree", 0);
        _log.Success("[Gallery] 资源管理器图库入口已隐藏");
        _explorer.Restart();
    }

    private void ShowExplorerGallery()
    {
        _reg.SetDword(RegistryHive.CurrentUser, GalleryKey, "System.IsPinnedToNameSpaceTree", 1);
        _log.Success("[Gallery] 资源管理器图库入口已恢复");
        _explorer.Restart();
    }

    private void DisableSnapFlyout()
    {
        _reg.SetDword(RegistryHive.CurrentUser, ExplorerAdvancedKey, "EnableSnapAssistFlyout", 0);
        _reg.SetDword(RegistryHive.CurrentUser, ExplorerAdvancedKey, "EnableSnapBar", 0);
        _log.Success("[Snap] 已关闭 Snap Layouts 悬停提示");
    }

    private void EnableSnapFlyout()
    {
        _reg.SetDword(RegistryHive.CurrentUser, ExplorerAdvancedKey, "EnableSnapAssistFlyout", 1);
        _reg.SetDword(RegistryHive.CurrentUser, ExplorerAdvancedKey, "EnableSnapBar", 1);
        _log.Success("[Snap] 已恢复 Snap Layouts 悬停提示");
    }

    private void HideExplorerHome()
    {
        _reg.SetDword(RegistryHive.CurrentUser, HomeKey, "System.IsPinnedToNameSpaceTree", 0);
        _log.Success("[Home] 资源管理器主页入口已隐藏");
        _explorer.Restart();
    }

    private void ShowExplorerHome()
    {
        _reg.SetDword(RegistryHive.CurrentUser, HomeKey, "System.IsPinnedToNameSpaceTree", 1);
        _log.Success("[Home] 资源管理器主页入口已恢复");
        _explorer.Restart();
    }

    private void DisableStartRecommendations()
    {
        _reg.SetDword(RegistryHive.CurrentUser, ExplorerAdvancedKey, "Start_IrisRecommendations", 0);
        _log.Success("[开始菜单] 已关闭推荐区域广告");
    }

    private void EnableStartRecommendations()
    {
        _reg.SetDword(RegistryHive.CurrentUser, ExplorerAdvancedKey, "Start_IrisRecommendations", 1);
        _log.Success("[开始菜单] 已恢复推荐区域广告");
    }

    private void EnableFolderContentsInfoTip()
    {
        _reg.SetDword(RegistryHive.CurrentUser, ExplorerAdvancedKey, "FolderContentsInfoTip", 1);
        _reg.SetDword(RegistryHive.CurrentUser, ExplorerAdvancedKey, "ShowInfoTip", 1);
        _log.Success("[文件夹提示] 已启用悬停详细信息");
    }

    private void DisableFolderContentsInfoTip()
    {
        _reg.SetDword(RegistryHive.CurrentUser, ExplorerAdvancedKey, "FolderContentsInfoTip", 0);
        _log.Success("[文件夹提示] 已关闭悬停详细信息");
    }

    private void DisableSearchHighlights()
    {
        _reg.SetDword(RegistryHive.CurrentUser, SearchHighlightsPolicyKey, "EnableDynamicContentInWSB", 0);
        _log.Success("[搜索高亮] 已关闭任务栏搜索高亮");
        _explorer.Restart();
    }

    private void EnableSearchHighlights()
    {
        _reg.DeleteValue(RegistryHive.CurrentUser, SearchHighlightsPolicyKey, "EnableDynamicContentInWSB");
        _log.Success("[搜索高亮] 已恢复任务栏搜索高亮");
        _explorer.Restart();
    }

    private void EnableTaskbarSeconds()
    {
        _reg.SetDword(RegistryHive.CurrentUser, ExplorerAdvancedKey, "ShowSecondsInSystemClock", 1);
        _log.Success("[任务栏时钟] 已显示秒数");
        _explorer.Restart();
    }

    private void DisableTaskbarSeconds()
    {
        _reg.SetDword(RegistryHive.CurrentUser, ExplorerAdvancedKey, "ShowSecondsInSystemClock", 0);
        _log.Success("[任务栏时钟] 已隐藏秒数");
        _explorer.Restart();
    }

    private void HideOneDriveNav()
    {
        _reg.SetDword(RegistryHive.CurrentUser, OneDriveKey, "System.IsPinnedToNameSpaceTree", 0);
        _log.Success("[OneDrive] 导航栏入口已隐藏");
        _explorer.Restart();
    }

    private void ShowOneDriveNav()
    {
        _reg.SetDword(RegistryHive.CurrentUser, OneDriveKey, "System.IsPinnedToNameSpaceTree", 1);
        _log.Success("[OneDrive] 导航栏入口已恢复");
        _explorer.Restart();
    }

    private void DisableLockScreenSpotlight()
    {
        _reg.SetDword(RegistryHive.CurrentUser, ContentDeliveryKey, "RotatingLockScreenEnabled", 0);
        _reg.SetDword(RegistryHive.CurrentUser, ContentDeliveryKey, "RotatingLockScreenOverlayEnabled", 0);
        _log.Success("[Spotlight] 锁屏 Spotlight 已关闭");
    }

    private void EnableLockScreenSpotlight()
    {
        _reg.SetDword(RegistryHive.CurrentUser, ContentDeliveryKey, "RotatingLockScreenEnabled", 1);
        _reg.SetDword(RegistryHive.CurrentUser, ContentDeliveryKey, "RotatingLockScreenOverlayEnabled", 1);
        _log.Success("[Spotlight] 锁屏 Spotlight 已恢复");
    }

    private void EnableFullPathInTitle()
    {
        _reg.SetDword(RegistryHive.CurrentUser, CabinetStateKey, "FullPath", 1);
        _log.Success("[完整路径] 标题栏已显示完整路径");
        _explorer.Restart();
    }

    private void DisableFullPathInTitle()
    {
        _reg.SetDword(RegistryHive.CurrentUser, CabinetStateKey, "FullPath", 0);
        _log.Success("[完整路径] 标题栏已恢复默认");
        _explorer.Restart();
    }

    private void EnableLongPaths()
    {
        _reg.SetDword(RegistryHive.LocalMachine, FileSystemKey, "LongPathsEnabled", 1);
        _log.Success("[长路径] 已启用（部分程序需重启后生效）");
    }

    private void DisableLongPaths()
    {
        _reg.SetDword(RegistryHive.LocalMachine, FileSystemKey, "LongPathsEnabled", 0);
        _log.Success("[长路径] 已关闭");
    }
}
