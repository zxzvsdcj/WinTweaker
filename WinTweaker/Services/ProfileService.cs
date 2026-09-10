using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using WinTweaker.Models;
using WinTweaker.Services;
using WinTweaker.ViewModels;

namespace WinTweaker.Services;

/// <summary>
/// 优化方案导出/导入。字典键兼容功能增删。
/// </summary>
public static class ProfileService
{
    public const int CurrentSchemaVersion = 1;
    public const string ProductCode = "win-tweaker";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static IReadOnlyList<ProfileFeature> Features { get; } = BuildFeatures();

    public static TweakerProfileDocument Capture(string name = "默认方案")
    {
        var doc = new TweakerProfileDocument
        {
            SchemaVersion = CurrentSchemaVersion,
            Product = ProductCode,
            ExportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Author = "微信号：zxzvsdcj",
            Name = name,
        };

        foreach (var feature in Features)
        {
            try
            {
                doc.Settings[feature.Key] = feature.Get();
            }
            catch
            {
                // 单项读取失败不阻断导出
            }
        }

        return doc;
    }

    public static void SaveToFile(TweakerProfileDocument doc, string path)
    {
        var json = JsonSerializer.Serialize(doc, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static TweakerProfileDocument LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        var doc = JsonSerializer.Deserialize<TweakerProfileDocument>(json, JsonOptions)
                  ?? throw new InvalidOperationException("方案文件为空或格式无效");

        if (!string.Equals(doc.Product, ProductCode, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(doc.Product))
        {
            throw new InvalidOperationException($"方案产品不匹配：{doc.Product}（需要 {ProductCode}）");
        }

        var raw = doc.Settings ?? new Dictionary<string, bool>();
        doc.Settings = new Dictionary<string, bool>(raw, StringComparer.OrdinalIgnoreCase);
        return doc;
    }

    public static ProfileApplyResult Apply(TweakerProfileDocument doc, bool suppressDangerConfirm = false)
    {
        var result = new ProfileApplyResult();
        var known = Features.ToDictionary(f => f.Key, f => f, StringComparer.OrdinalIgnoreCase);

        foreach (var key in doc.Settings.Keys)
        {
            if (!known.ContainsKey(key))
            {
                result.SkippedUnknown++;
                result.UnknownKeys.Add(key);
            }
        }

        var dangerKeys = doc.Settings
            .Where(kv => known.TryGetValue(kv.Key, out var f) && f.IsDanger && kv.Value)
            .Select(kv => known[kv.Key].DisplayName)
            .ToList();

        if (dangerKeys.Count > 0 && !suppressDangerConfirm)
        {
            var msg = "方案包含以下高危项（将开启）：\n\n• " +
                      string.Join("\n• ", dangerKeys) +
                      "\n\n确定应用？";
            var confirm = MessageBox.Show(msg, "高危配置确认", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
            {
                LogService.Instance.Warning("用户取消了含高危项的方案导入");
                return result;
            }
            suppressDangerConfirm = true;
        }

        try
        {
            ProfileApplyContext.SuppressDangerConfirm = suppressDangerConfirm;

            foreach (var feature in Features)
            {
                if (!doc.Settings.TryGetValue(feature.Key, out var desired))
                    continue;

                if (!feature.IsSupported())
                {
                    result.SkippedUnsupported++;
                    continue;
                }

                bool current;
                try
                {
                    current = feature.Get();
                }
                catch
                {
                    result.Failed++;
                    result.FailedKeys.Add(feature.Key);
                    continue;
                }

                if (current == desired)
                {
                    result.Unchanged++;
                    continue;
                }

                try
                {
                    feature.Set(desired);
                    result.Applied++;
                    result.AppliedKeys.Add(feature.Key);
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.FailedKeys.Add(feature.Key);
                    LogService.Instance.Error($"方案应用失败 {feature.Key}: {ex.Message}");
                }
            }
        }
        finally
        {
            ProfileApplyContext.SuppressDangerConfirm = false;
        }

        return result;
    }

    public static string? ExportWithDialog()
    {
        var dlg = new SaveFileDialog
        {
            Title = "导出优化方案",
            Filter = "WinTweaker 方案 (*.json)|*.json",
            FileName = $"WinTweaker-方案-{DateTime.Now:yyyyMMdd-HHmm}.json",
            DefaultExt = ".json",
            AddExtension = true,
        };

        if (dlg.ShowDialog() != true)
            return null;

        var doc = Capture(Path.GetFileNameWithoutExtension(dlg.FileName));
        SaveToFile(doc, dlg.FileName);
        LogService.Instance.Success($"已导出方案：{dlg.FileName}（{doc.Settings.Count} 项）");
        return dlg.FileName;
    }

    public static ProfileApplyResult? ImportWithDialog()
    {
        var dlg = new OpenFileDialog
        {
            Title = "导入优化方案",
            Filter = "WinTweaker 方案 (*.json)|*.json",
            DefaultExt = ".json",
            CheckFileExists = true,
        };

        if (dlg.ShowDialog() != true)
            return null;

        var doc = LoadFromFile(dlg.FileName);
        var result = Apply(doc);
        LogService.Instance.Info($"导入方案：{dlg.FileName}");
        LogService.Instance.Info(result.ToSummary());
        if (result.UnknownKeys.Count > 0)
            LogService.Instance.Warning("跳过未知键：" + string.Join(", ", result.UnknownKeys));
        return result;
    }

    private static List<ProfileFeature> BuildFeatures()
    {
        return
        [
            Feat("general.ultimatePower", "卓越性能电源计划", () => ViewModelLocator.General.IsUltimatePowerEnabled, v => ViewModelLocator.General.IsUltimatePowerEnabled = v),
            Feat("general.telemetryReduced", "系统遥测降级", () => ViewModelLocator.General.IsTelemetryReduced, v => ViewModelLocator.General.IsTelemetryReduced = v),
            Feat("general.adsDisabled", "关闭系统广告", () => ViewModelLocator.General.IsAdsDisabled, v => ViewModelLocator.General.IsAdsDisabled = v),
            Feat("general.servicesOptimized", "裁剪冗余服务", () => ViewModelLocator.General.IsServicesOptimized, v => ViewModelLocator.General.IsServicesOptimized = v),
            Feat("general.backgroundDisabled", "禁止后台运行", () => ViewModelLocator.General.IsBackgroundDisabled, v => ViewModelLocator.General.IsBackgroundDisabled = v),
            Feat("general.hibernationDisabled", "关闭休眠", () => ViewModelLocator.General.IsHibernationDisabled, v => ViewModelLocator.General.IsHibernationDisabled = v),
            Feat("general.explorerOptimized", "资源管理器优化", () => ViewModelLocator.General.IsExplorerOptimized, v => ViewModelLocator.General.IsExplorerOptimized = v),
            Feat("general.fileExtensionsShown", "显示文件扩展名", () => ViewModelLocator.General.IsFileExtensionsShown, v => ViewModelLocator.General.IsFileExtensionsShown = v),
            Feat("general.reservedStorageDisabled", "禁用保留存储", () => ViewModelLocator.General.IsReservedStorageDisabled, v => ViewModelLocator.General.IsReservedStorageDisabled = v),
            Feat("general.memoryIntegrityEnabled", "内存完整性（HVCI）",
                () => ViewModelLocator.General.IsMemoryIntegrityEnabled,
                v => ViewModelLocator.General.IsMemoryIntegrityEnabled = v,
                () => ViewModelLocator.General.CanToggleMemoryIntegrity,
                isDanger: true),

            Feat("update.edgeUpdateDisabled", "禁止 Edge 自动更新", () => ViewModelLocator.Update.IsEdgeUpdateDisabled, v => ViewModelLocator.Update.IsEdgeUpdateDisabled = v),
            Feat("update.chromeUpdateDisabled", "禁止 Chrome 自动更新", () => ViewModelLocator.Update.IsChromeUpdateDisabled, v => ViewModelLocator.Update.IsChromeUpdateDisabled = v),

            Feat("win11.classicContextMenu", "经典右键菜单", () => ViewModelLocator.Win11.IsClassicContextMenuEnabled, v => ViewModelLocator.Win11.IsClassicContextMenuEnabled = v, () => ViewModelLocator.Win11.CanClassicContextMenu),
            Feat("win11.taskbarLeftAlign", "任务栏左对齐", () => ViewModelLocator.Win11.IsTaskbarLeftAligned, v => ViewModelLocator.Win11.IsTaskbarLeftAligned = v, () => ViewModelLocator.Win11.CanTaskbarLeftAlign),
            Feat("win11.taskbarEndTask", "任务栏结束任务", () => ViewModelLocator.Win11.IsTaskbarEndTaskEnabled, v => ViewModelLocator.Win11.IsTaskbarEndTaskEnabled = v, () => ViewModelLocator.Win11.CanTaskbarEndTask),
            Feat("win11.taskbarSeconds", "任务栏时钟显示秒", () => ViewModelLocator.Win11.IsTaskbarSecondsEnabled, v => ViewModelLocator.Win11.IsTaskbarSecondsEnabled = v, () => ViewModelLocator.Win11.CanShowTaskbarSeconds),
            Feat("win11.hideGallery", "隐藏 Gallery", () => ViewModelLocator.Win11.IsExplorerGalleryHidden, v => ViewModelLocator.Win11.IsExplorerGalleryHidden = v, () => ViewModelLocator.Win11.CanHideExplorerGallery),
            Feat("win11.hideHome", "隐藏 Home", () => ViewModelLocator.Win11.IsExplorerHomeHidden, v => ViewModelLocator.Win11.IsExplorerHomeHidden = v, () => ViewModelLocator.Win11.CanHideExplorerHome),
            Feat("win11.hideOneDriveNav", "隐藏 OneDrive 导航栏", () => ViewModelLocator.Win11.IsOneDriveNavHidden, v => ViewModelLocator.Win11.IsOneDriveNavHidden = v, () => ViewModelLocator.Win11.CanHideOneDriveNav),
            Feat("win11.fullPathInTitle", "标题栏完整路径", () => ViewModelLocator.Win11.IsFullPathInTitleEnabled, v => ViewModelLocator.Win11.IsFullPathInTitleEnabled = v, () => ViewModelLocator.Win11.CanShowFullPathInTitle),
            Feat("win11.folderInfoTip", "文件夹悬停详细信息", () => ViewModelLocator.Win11.IsFolderContentsInfoTipEnabled, v => ViewModelLocator.Win11.IsFolderContentsInfoTipEnabled = v, () => ViewModelLocator.Win11.CanFolderContentsInfoTip),
            Feat("win11.snapFlyoutDisabled", "禁用 Snap 悬停提示", () => ViewModelLocator.Win11.IsSnapFlyoutDisabled, v => ViewModelLocator.Win11.IsSnapFlyoutDisabled = v, () => ViewModelLocator.Win11.CanDisableSnapFlyout),
            Feat("win11.startRecommendationsDisabled", "关闭开始菜单推荐广告", () => ViewModelLocator.Win11.IsStartRecommendationsDisabled, v => ViewModelLocator.Win11.IsStartRecommendationsDisabled = v, () => ViewModelLocator.Win11.CanDisableStartRecommendations),
            Feat("win11.searchHighlightsDisabled", "禁用搜索高亮", () => ViewModelLocator.Win11.IsSearchHighlightsDisabled, v => ViewModelLocator.Win11.IsSearchHighlightsDisabled = v, () => ViewModelLocator.Win11.CanDisableSearchHighlights),
            Feat("win11.lockScreenSpotlightDisabled", "禁用锁屏 Spotlight", () => ViewModelLocator.Win11.IsLockScreenSpotlightDisabled, v => ViewModelLocator.Win11.IsLockScreenSpotlightDisabled = v, () => ViewModelLocator.Win11.CanDisableLockScreenSpotlight),
            Feat("win11.longPathsEnabled", "启用 Win32 长路径", () => ViewModelLocator.Win11.IsLongPathsEnabled, v => ViewModelLocator.Win11.IsLongPathsEnabled = v, () => ViewModelLocator.Win11.CanEnableLongPaths),
            Feat("win11.copilotDisabled", "禁用 Copilot", () => ViewModelLocator.Win11.IsCopilotDisabled, v => ViewModelLocator.Win11.IsCopilotDisabled = v, () => ViewModelLocator.Win11.CanDisableCopilot),
            Feat("win11.widgetsDisabled", "禁用 Widgets", () => ViewModelLocator.Win11.IsWidgetsDisabled, v => ViewModelLocator.Win11.IsWidgetsDisabled = v, () => ViewModelLocator.Win11.CanDisableWidgets),
            Feat("win11.taskbarBingDisabled", "关闭必应搜索", () => ViewModelLocator.Win11.IsTaskbarBingDisabled, v => ViewModelLocator.Win11.IsTaskbarBingDisabled = v, () => ViewModelLocator.Win11.CanDisableTaskbarBing),

            Feat("danger.uacDisabled", "关闭 UAC", () => ViewModelLocator.Danger.IsUacDisabled, v => ViewModelLocator.Danger.IsUacDisabled = v, isDanger: true),
            Feat("danger.firewallDisabled", "关闭防火墙", () => ViewModelLocator.Danger.IsFirewallDisabled, v => ViewModelLocator.Danger.IsFirewallDisabled = v, isDanger: true),
            Feat("danger.defenderSuppressed", "压制 Defender", () => ViewModelLocator.Danger.IsDefenderSuppressed, v => ViewModelLocator.Danger.IsDefenderSuppressed = v, isDanger: true),
            Feat("danger.windowsUpdateDisabled", "禁止 Windows 更新", () => ViewModelLocator.Danger.IsWindowsUpdateDisabled, v => ViewModelLocator.Danger.IsWindowsUpdateDisabled = v, isDanger: true),
        ];
    }

    private static ProfileFeature Feat(
        string key,
        string displayName,
        Func<bool> get,
        Action<bool> set,
        Func<bool>? supported = null,
        bool isDanger = false)
        => new(key, displayName, get, set, supported ?? (() => true), isDanger);
}

public sealed class ProfileFeature
{
    public string Key { get; }
    public string DisplayName { get; }
    public bool IsDanger { get; }
    private readonly Func<bool> _get;
    private readonly Action<bool> _set;
    private readonly Func<bool> _supported;

    public ProfileFeature(string key, string displayName, Func<bool> get, Action<bool> set, Func<bool> supported, bool isDanger)
    {
        Key = key;
        DisplayName = displayName;
        IsDanger = isDanger;
        _get = get;
        _set = set;
        _supported = supported;
    }

    public bool Get() => _get();
    public void Set(bool value) => _set(value);
    public bool IsSupported() => _supported();
}

/// <summary>方案批量导入时抑制逐项高危确认（已在导入入口统一确认）。</summary>
internal static class ProfileApplyContext
{
    public static bool SuppressDangerConfirm { get; set; }
}
