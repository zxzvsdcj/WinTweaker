using System.Runtime.InteropServices;
using System.ServiceProcess;
using Microsoft.Win32;

namespace WinTweaker.Services;

/// <summary>
/// 服务管理器 —— 纯 C# API 操作系统服务
/// 核心机制：操作前缓存原始启动类型，回滚时精准还原
/// </summary>
public sealed class ServiceManager
{
    private static readonly Lazy<ServiceManager> _instance = new(() => new ServiceManager());
    public static ServiceManager Instance => _instance.Value;

    /// <summary>
    /// 原始启动类型缓存 key=服务名, value=原始启动类型
    /// 优化前读取并存入，回滚时从此字典恢复
    /// </summary>
    private readonly Dictionary<string, ServiceStartMode> _originalStartTypes = new();

    private readonly object _lock = new();
    private readonly LogService _log = LogService.Instance;

    private ServiceManager() { }

    /// <summary>
    /// 获取服务当前启动类型
    /// </summary>
    public ServiceStartMode? GetStartType(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            return sc.StartType;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 检查服务是否存在
    /// </summary>
    public bool ServiceExists(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            _ = sc.Status;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 禁用服务（优化操作）
    /// 自动缓存原始启动类型
    /// </summary>
    public bool DisableService(string serviceName)
    {
        try
        {
            if (!ServiceExists(serviceName))
            {
                _log.Warning($"服务 [{serviceName}] 不存在，跳过");
                return false;
            }

            CacheOriginalStartType(serviceName);

            if (!SetServiceStartType(serviceName, ServiceStartMode.Disabled))
                return false;

            StopServiceIfRunning(serviceName);
            _log.Success($"服务 [{serviceName}] 已禁用");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"禁用服务 [{serviceName}] 失败：{ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 恢复服务到原始启动类型（回滚操作）
    /// 从内存缓存读取，不硬编码
    /// </summary>
    public bool RestoreService(string serviceName)
    {
        try
        {
            lock (_lock)
            {
                if (!_originalStartTypes.TryGetValue(serviceName, out var originalType))
                {
                    _log.Warning($"服务 [{serviceName}] 无缓存的原始启动类型，跳过恢复");
                    return false;
                }

                if (!SetServiceStartType(serviceName, originalType))
                    return false;

                // 如果原始状态不是禁用，尝试启动服务
                if (originalType != ServiceStartMode.Disabled)
                {
                    StartServiceIfStopped(serviceName);
                }

                _log.Success($"服务 [{serviceName}] 已恢复为 [{originalType}]");
                return true;
            }
        }
        catch (Exception ex)
        {
            _log.Error($"恢复服务 [{serviceName}] 失败：{ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 批量禁用服务
    /// </summary>
    public void DisableServices(params string[] serviceNames)
    {
        foreach (var name in serviceNames)
        {
            DisableService(name);
        }
    }

    /// <summary>
    /// 批量恢复服务
    /// </summary>
    public void RestoreServices(params string[] serviceNames)
    {
        foreach (var name in serviceNames)
        {
            RestoreService(name);
        }
    }

    /// <summary>
    /// 获取已缓存的原始启动类型（调试/UI展示用）
    /// </summary>
    public IReadOnlyDictionary<string, ServiceStartMode> GetCachedOriginalTypes()
    {
        lock (_lock)
        {
            return new Dictionary<string, ServiceStartMode>(_originalStartTypes);
        }
    }

    /// <summary>
    /// 缓存服务原始启动类型（仅首次缓存，不覆盖）
    /// </summary>
    private void CacheOriginalStartType(string serviceName)
    {
        lock (_lock)
        {
            if (_originalStartTypes.ContainsKey(serviceName))
                return;

            var startType = GetStartType(serviceName);
            if (startType.HasValue)
            {
                _originalStartTypes[serviceName] = startType.Value;
                _log.Info($"已缓存服务 [{serviceName}] 原始启动类型：{startType.Value}");
            }
        }
    }

    /// <summary>
    /// 通过注册表设置服务启动类型
    /// 纯 C# 实现，无需 sc.exe 命令
    /// </summary>
    private bool SetServiceStartType(string serviceName, ServiceStartMode startMode)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                $@"SYSTEM\CurrentControlSet\Services\{serviceName}", writable: true);

            if (key == null)
            {
                _log.Error($"无法打开服务注册表项：{serviceName}");
                return false;
            }

            int startValue = startMode switch
            {
                ServiceStartMode.Automatic => 2,
                ServiceStartMode.Manual => 3,
                ServiceStartMode.Disabled => 4,
                _ => 3
            };

            key.SetValue("Start", startValue, RegistryValueKind.DWord);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"设置服务 [{serviceName}] 启动类型失败：{ex.Message}");
            return false;
        }
    }

    private void StopServiceIfRunning(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            if (sc.Status == ServiceControllerStatus.Running ||
                sc.Status == ServiceControllerStatus.StartPending)
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
            }
        }
        catch
        {
            // 停止服务失败不阻塞流程，下次重启后生效
        }
    }

    private void StartServiceIfStopped(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            if (sc.Status == ServiceControllerStatus.Stopped)
            {
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
            }
        }
        catch
        {
            // 启动服务失败不阻塞，可能需要重启
        }
    }
}
