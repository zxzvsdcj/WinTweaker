using System.IO;

namespace WinTweaker.License;

/// <summary>
/// 完整授权校验流程（对齐 Python check_license / Rust check_license_flow）。
/// </summary>
public sealed class LicenseCheckResult
{
    public string Status { get; init; } = "not_activated";
    public string MachineCode { get; init; } = "";
    public string Message { get; init; } = "";
    public long GraceDaysLeft { get; init; }
    public string LicenseType { get; init; } = "永久授权";
    public long ExpireTime { get; init; }
    public long StartTime { get; init; }
    public long RemainingDays { get; init; } = -1;
    public bool IsOffline { get; init; }

    public bool Ok => Status is "activated" or "offline_grace";
}

public static class LicenseClient
{
    public static async Task<LicenseCheckResult> CheckLicenseAsync(
        string? configPath = null,
        CancellationToken ct = default)
    {
        var machineCode = MachineCode.Generate();

        LicenseConfig cfg;
        try
        {
            cfg = LicenseConfig.Load(configPath);
        }
        catch (FileNotFoundException ex)
        {
            return new LicenseCheckResult
            {
                Status = "not_configured",
                MachineCode = machineCode,
                Message = ex.Message,
            };
        }
        catch (InvalidOperationException ex)
        {
            return new LicenseCheckResult
            {
                Status = "not_configured",
                MachineCode = machineCode,
                Message = ex.Message,
            };
        }

        if (string.IsNullOrWhiteSpace(cfg.AppId))
        {
            return new LicenseCheckResult
            {
                Status = "not_configured",
                MachineCode = machineCode,
                Message = "未配置飞书凭证",
            };
        }

        var client = new FeishuLicenseClient(cfg);
        FeishuLicenseClient.OnlineStatus status;
        try
        {
            status = await client.VerifyMachineAsync(machineCode, ct).ConfigureAwait(false);
        }
        catch (Exception err)
        {
            var verdict = LicenseCacheStore.OfflineVerify(machineCode, cfg.CacheMaterial());
            if (verdict.Allowed)
            {
                var rem = FeishuLicenseClient.RemainingDays(
                    verdict.Cache.LicenseType, verdict.Cache.ExpireTime);
                return new LicenseCheckResult
                {
                    Status = "offline_grace",
                    MachineCode = machineCode,
                    Message = $"网络不可用，离线宽限期剩余 {verdict.GraceDaysLeft} 天（{err.Message}）",
                    GraceDaysLeft = verdict.GraceDaysLeft,
                    LicenseType = verdict.Cache.LicenseType,
                    ExpireTime = verdict.Cache.ExpireTime,
                    StartTime = verdict.Cache.StartTime,
                    RemainingDays = rem,
                    IsOffline = true,
                };
            }

            if (verdict.Reason == "expired")
            {
                return new LicenseCheckResult
                {
                    Status = "expired",
                    MachineCode = machineCode,
                    Message = "授权已到期（离线校验），请联网续期后重试",
                    LicenseType = verdict.Cache.LicenseType,
                    ExpireTime = verdict.Cache.ExpireTime,
                    RemainingDays = 0,
                    IsOffline = true,
                };
            }

            return new LicenseCheckResult
            {
                Status = "network_error",
                MachineCode = machineCode,
                Message = $"无法连接授权服务器且无有效离线缓存：{err.Message}",
            };
        }

        if (status.Kind == "licensed" && status.Info is not null)
        {
            var info = status.Info;
            LicenseCacheStore.Save(
                machineCode, info.LicenseType, info.ExpireTime, info.StartTime, info.GrantDays, cfg.CacheMaterial());
            return new LicenseCheckResult
            {
                Status = "activated",
                MachineCode = machineCode,
                Message = "授权验证通过",
                LicenseType = info.LicenseType,
                ExpireTime = info.ExpireTime,
                StartTime = info.StartTime,
                RemainingDays = FeishuLicenseClient.RemainingDays(info.LicenseType, info.ExpireTime),
            };
        }

        if (status.Kind == "expired" && status.Info is not null)
        {
            var info = status.Info;
            LicenseCacheStore.Save(
                machineCode, info.LicenseType, info.ExpireTime, info.StartTime, info.GrantDays, cfg.CacheMaterial());
            return new LicenseCheckResult
            {
                Status = "expired",
                MachineCode = machineCode,
                Message = "授权已到期，请联系管理员续期",
                LicenseType = info.LicenseType,
                ExpireTime = info.ExpireTime,
                StartTime = info.StartTime,
                RemainingDays = 0,
            };
        }

        if (status.Kind == "blacklisted")
        {
            LicenseCacheStore.Clear();
            return new LicenseCheckResult
            {
                Status = "blacklisted",
                MachineCode = machineCode,
                Message = "该设备授权已被停用，请联系管理员处理",
            };
        }

        LicenseCacheStore.Clear();
        return new LicenseCheckResult
        {
            Status = "not_activated",
            MachineCode = machineCode,
            Message = "本机尚未激活，请将机器码发送给管理员完成授权",
        };
    }

    public static LicenseCheckResult CheckLicense(string? configPath = null)
        => Task.Run(() => CheckLicenseAsync(configPath)).GetAwaiter().GetResult();
}
