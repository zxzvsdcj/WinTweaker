using System.Runtime.InteropServices;
using Microsoft.Win32;
using WinTweaker.Models;

namespace WinTweaker.Services;

/// <summary>
/// 系统版本识别核心服务
/// 所有功能模块的分支判断依赖此服务
/// 启动时一次性检测，结果缓存为单例
/// </summary>
public sealed class SystemInfoService
{
    private static readonly Lazy<SystemInfoService> _instance = new(() => new SystemInfoService());
    public static SystemInfoService Instance => _instance.Value;

    public SystemInfo Current { get; }

    private SystemInfoService()
    {
        Current = DetectSystemInfo();
    }

    private static SystemInfo DetectSystemInfo()
    {
        int buildNumber = GetBuildNumber();
        var majorVersion = DetermineMajorVersion(buildNumber);
        var edition = DetectEdition();
        string displayVersion = GetDisplayVersion();
        bool isInsider = DetectInsiderPreview();

        string fullVersion = BuildFullVersionString(majorVersion, edition, displayVersion, buildNumber, isInsider);

        return new SystemInfo
        {
            MajorVersion = majorVersion,
            BuildNumber = buildNumber,
            DisplayVersion = displayVersion,
            Edition = edition,
            IsInsiderPreview = isInsider,
            FullVersionString = fullVersion
        };
    }

    /// <summary>
    /// 获取 OS Build 号
    /// 通过注册表 CurrentBuildNumber 读取（最可靠）
    /// </summary>
    private static int GetBuildNumber()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key == null) return 0;

            string? buildStr = key.GetValue("CurrentBuildNumber") as string;
            if (int.TryParse(buildStr, out int build))
                return build;
        }
        catch
        {
            // 回退到 Environment.OSVersion
        }

        return Environment.OSVersion.Version.Build;
    }

    /// <summary>
    /// 根据 Build 号判断 Win10/Win11
    /// Win11 起始 Build: 22000
    /// Win10 支持范围: 18362(1903) ~ 19045(22H2)
    /// 低于 18362 为不支持版本
    /// </summary>
    private static WindowsMajorVersion DetermineMajorVersion(int buildNumber)
    {
        if (buildNumber >= 22000)
            return WindowsMajorVersion.Windows11;

        // Win10 1903 = Build 18362
        if (buildNumber >= 18362)
            return WindowsMajorVersion.Windows10;

        return WindowsMajorVersion.Unsupported;
    }

    /// <summary>
    /// 检测 Windows 版本（家庭版/专业版/企业版/教育版）
    /// 通过 GetProductInfo API 获取 SKU
    /// </summary>
    private static WindowsEdition DetectEdition()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            string? editionId = key?.GetValue("EditionID") as string;

            return editionId?.ToUpperInvariant() switch
            {
                "CORE" or "COREN" => WindowsEdition.Home,
                "CORECOUNTRYSPECIFIC" => WindowsEdition.Home,
                "CORESINGLELANGUAGE" => WindowsEdition.Home,
                "PROFESSIONAL" or "PROFESSIONALEDUCATION" => WindowsEdition.Professional,
                "ENTERPRISE" or "ENTERPRISES" or "ENTERPRISEEVALUATION" => WindowsEdition.Enterprise,
                "EDUCATION" => WindowsEdition.Education,
                _ => WindowsEdition.Unknown
            };
        }
        catch
        {
            return WindowsEdition.Unknown;
        }
    }

    /// <summary>
    /// 获取显示版本号（如 "23H2"、"24H2"）
    /// </summary>
    private static string GetDisplayVersion()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            return key?.GetValue("DisplayVersion") as string ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    /// <summary>
    /// 检测是否为 Insider 预览版
    /// 通过注册表 BuildLabEx 或 BuildBranch 判断
    /// </summary>
    private static bool DetectInsiderPreview()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key == null) return false;

            // 检查 BuildBranch 是否包含预览版标识
            string? buildBranch = key.GetValue("BuildBranch") as string;
            if (!string.IsNullOrEmpty(buildBranch))
            {
                string upper = buildBranch.ToUpperInvariant();
                if (upper.Contains("_PRERELEASE") || upper.Contains("DEV") || 
                    upper.Contains("CANARY") || upper.Contains("BETA"))
                    return true;
            }

            // 26200+ 且非正式 GA 发布的版本视为预览版
            string? ubr = key.GetValue("UBR")?.ToString();
            int buildNumber = GetBuildNumber();
            if (buildNumber >= 26200)
            {
                // 25H2 Insider 的典型 Build 范围
                string? productName = key.GetValue("ProductName") as string;
                if (productName != null && productName.Contains("Insider"))
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildFullVersionString(
        WindowsMajorVersion major, WindowsEdition edition,
        string displayVersion, int buildNumber, bool isInsider)
    {
        string osName = major switch
        {
            WindowsMajorVersion.Windows11 => "Windows 11",
            WindowsMajorVersion.Windows10 => "Windows 10",
            _ => "Windows (不支持)"
        };

        string editionName = edition switch
        {
            WindowsEdition.Home => "家庭版",
            WindowsEdition.Professional => "专业版",
            WindowsEdition.Enterprise => "企业版",
            WindowsEdition.Education => "教育版",
            _ => ""
        };

        string insider = isInsider ? " [Insider Preview]" : "";
        return $"{osName} {editionName} {displayVersion} (Build {buildNumber}){insider}";
    }

    /// <summary>
    /// 验证系统是否受支持，不支持时返回错误信息
    /// </summary>
    public (bool isSupported, string? errorMessage) ValidateSystemSupport()
    {
        if (Current.MajorVersion == WindowsMajorVersion.Unsupported)
        {
            return (false, $"当前系统 (Build {Current.BuildNumber}) 不受支持。\n" +
                          "本工具仅支持 Windows 10 (1903+) 和 Windows 11。\n" +
                          "Win7/Win8/Server 版本无法使用。");
        }
        return (true, null);
    }
}
