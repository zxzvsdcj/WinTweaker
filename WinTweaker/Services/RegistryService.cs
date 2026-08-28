using Microsoft.Win32;

namespace WinTweaker.Services;

/// <summary>
/// 注册表服务 —— 封装所有注册表读写操作
/// 纯 C# 实现，提供安全的读写封装
/// </summary>
public sealed class RegistryService
{
    private static readonly Lazy<RegistryService> _instance = new(() => new RegistryService());
    public static RegistryService Instance => _instance.Value;

    private readonly LogService _log = LogService.Instance;

    private RegistryService() { }

    /// <summary>
    /// 设置 DWORD 值
    /// </summary>
    public bool SetDword(RegistryHive hive, string subKey, string valueName, int value)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var key = baseKey.CreateSubKey(subKey, writable: true);
            if (key == null)
            {
                _log.Error($"无法创建注册表路径：{hive}\\{subKey}");
                return false;
            }
            key.SetValue(valueName, value, RegistryValueKind.DWord);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"写入注册表失败 [{subKey}\\{valueName}]：{ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 设置字符串值
    /// </summary>
    public bool SetString(RegistryHive hive, string subKey, string valueName, string value)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var key = baseKey.CreateSubKey(subKey, writable: true);
            if (key == null)
            {
                _log.Error($"无法创建注册表路径：{hive}\\{subKey}");
                return false;
            }
            key.SetValue(valueName, value, RegistryValueKind.String);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"写入注册表失败 [{subKey}\\{valueName}]：{ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 读取 DWORD 值
    /// </summary>
    public int? GetDword(RegistryHive hive, string subKey, string valueName)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(subKey);
            var value = key?.GetValue(valueName);
            if (value is int intVal) return intVal;
            if (value != null && int.TryParse(value.ToString(), out int parsed)) return parsed;
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 读取字符串值
    /// </summary>
    public string? GetString(RegistryHive hive, string subKey, string valueName)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(subKey);
            return key?.GetValue(valueName) as string;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 删除注册表值
    /// </summary>
    public bool DeleteValue(RegistryHive hive, string subKey, string valueName)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(subKey, writable: true);
            if (key == null) return true;
            key.DeleteValue(valueName, throwOnMissingValue: false);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"删除注册表值失败 [{subKey}\\{valueName}]：{ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 检查注册表值是否存在
    /// </summary>
    public bool ValueExists(RegistryHive hive, string subKey, string valueName)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(subKey);
            return key?.GetValue(valueName) != null;
        }
        catch
        {
            return false;
        }
    }
}
