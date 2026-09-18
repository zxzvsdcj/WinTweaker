using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WinTweaker.License;

/// <summary>
/// 离线授权缓存：AES-256-GCM。密钥 = SHA256("wuguard-license-cache-v2|" + 内嵌材料 + "|" + 机器码)，
/// 正文另带 HMAC-SHA256。只知道机器码无法伪造。路径：%APPDATA%/WuGuard/license.dat，宽限 7 天。
/// 升级后旧缓存无法解密，需再成功在线验证一次。
/// </summary>
public static class LicenseCacheStore
{
    public const int GraceDays = 7;
    public const long PermanentExpireMs = 4102415999000L;

    public sealed class LicenseCache
    {
        public string MachineCode { get; set; } = "";
        public long VerifiedAt { get; set; }
        public string LicenseType { get; set; } = "永久授权";
        public long ExpireTime { get; set; }
        public long StartTime { get; set; }
        public long GrantDays { get; set; }
        public string Mac { get; set; } = "";
    }

    public sealed class OfflineVerdict
    {
        public bool Allowed { get; init; }
        public long GraceDaysLeft { get; init; }
        public LicenseCache Cache { get; init; } = new();
        public string Reason { get; init; } = "";
    }

    public static string CachePath()
    {
        var overridePath = Environment.GetEnvironmentVariable("WUGUARD_LICENSE_CACHE");
        if (!string.IsNullOrWhiteSpace(overridePath))
            return overridePath;
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(baseDir))
            baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        var directory = Path.Combine(baseDir, "WuGuard");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "license.dat");
    }

    private static byte[] DeriveKey(string material, string machineCode)
    {
        var seed = Encoding.UTF8.GetBytes($"wuguard-license-cache-v2|{material}|{machineCode}");
        return SHA256.HashData(seed);
    }

    private static byte[] MacKey(string material)
    {
        var seed = Encoding.UTF8.GetBytes($"wuguard-license-mac-v2|{material}");
        return SHA256.HashData(seed);
    }

    private static string Canonical(LicenseCache cache)
        => $"v2|{cache.MachineCode}|{cache.VerifiedAt}|{cache.LicenseType}|{cache.ExpireTime}|{cache.StartTime}|{cache.GrantDays}";

    private static string ComputeMac(string material, string canonical)
    {
        using var hmac = new HMACSHA256(MacKey(material));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public static void Save(
        string machineCode,
        string licenseType,
        long expireTime,
        long startTime,
        long grantDays,
        string material)
    {
        var verifiedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var cache = new LicenseCache
        {
            MachineCode = machineCode,
            VerifiedAt = verifiedAt,
            LicenseType = licenseType,
            ExpireTime = expireTime,
            StartTime = startTime,
            GrantDays = grantDays,
        };
        cache.Mac = ComputeMac(material, Canonical(cache));
        var payload = new Dictionary<string, object?>
        {
            ["machine_code"] = cache.MachineCode,
            ["verified_at"] = cache.VerifiedAt,
            ["license_type"] = cache.LicenseType,
            ["expire_time"] = cache.ExpireTime,
            ["start_time"] = cache.StartTime,
            ["grant_days"] = cache.GrantDays,
            ["mac"] = cache.Mac,
        };
        var plaintext = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        var key = DeriveKey(material, machineCode);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(key, 16))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        var blob = new byte[nonce.Length + ciphertext.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, blob, 0, nonce.Length);
        Buffer.BlockCopy(ciphertext, 0, blob, nonce.Length, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, blob, nonce.Length + ciphertext.Length, tag.Length);
        var path = CachePath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllBytes(path, blob);
    }

    public static LicenseCache? Read(string machineCode, string material)
    {
        var path = CachePath();
        if (!File.Exists(path))
            return null;

        var data = File.ReadAllBytes(path);
        if (data.Length < 12 + 16)
            return null;

        try
        {
            var key = DeriveKey(material, machineCode);
            var nonce = data.AsSpan(0, 12);
            var tag = data.AsSpan(data.Length - 16, 16);
            var ciphertext = data.AsSpan(12, data.Length - 12 - 16);
            var plaintext = new byte[ciphertext.Length];
            using (var aes = new AesGcm(key, 16))
            {
                aes.Decrypt(nonce, ciphertext, tag, plaintext);
            }

            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(plaintext));
            var root = doc.RootElement;
            var cachedCode = root.GetProperty("machine_code").GetString() ?? "";
            if (cachedCode != machineCode)
                return null;

            var mac = root.TryGetProperty("mac", out var macEl) ? (macEl.GetString() ?? "") : "";
            if (string.IsNullOrEmpty(mac))
                return null;

            var parsed = new LicenseCache
            {
                MachineCode = cachedCode,
                VerifiedAt = root.TryGetProperty("verified_at", out var va) ? va.GetInt64() : 0,
                LicenseType = root.TryGetProperty("license_type", out var lt)
                    ? (lt.GetString() ?? "永久授权")
                    : "永久授权",
                ExpireTime = root.TryGetProperty("expire_time", out var et) ? et.GetInt64() : 0,
                StartTime = root.TryGetProperty("start_time", out var st) ? st.GetInt64() : 0,
                GrantDays = root.TryGetProperty("grant_days", out var gd) ? gd.GetInt64() : 0,
                Mac = mac,
            };
            var expect = ComputeMac(material, Canonical(parsed));
            var left = Encoding.UTF8.GetBytes(mac);
            var right = Encoding.UTF8.GetBytes(expect);
            if (!CryptographicOperations.FixedTimeEquals(left, right))
                return null;
            return parsed;
        }
        catch
        {
            return null;
        }
    }

    public static void Clear()
    {
        try
        {
            var path = CachePath();
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            /* ignore */
        }
    }

    public static OfflineVerdict OfflineVerify(string machineCode, string material)
    {
        var empty = new LicenseCache { MachineCode = machineCode, VerifiedAt = 0 };
        var cache = Read(machineCode, material);
        if (cache is null)
            return new OfflineVerdict { Allowed = false, GraceDaysLeft = 0, Cache = empty, Reason = "invalid" };

        var nowSecs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nowMs = nowSecs * 1000;
        var permanent = cache.LicenseType == "永久授权";
        if (!permanent && cache.ExpireTime > 0 && nowMs > cache.ExpireTime)
            return new OfflineVerdict { Allowed = false, GraceDaysLeft = 0, Cache = cache, Reason = "expired" };

        var elapsedDays = (nowSecs - cache.VerifiedAt) / 86400;
        var graceLeft = GraceDays - elapsedDays;
        if (graceLeft > 0)
            return new OfflineVerdict { Allowed = true, GraceDaysLeft = graceLeft, Cache = cache, Reason = "" };

        return new OfflineVerdict { Allowed = false, GraceDaysLeft = 0, Cache = cache, Reason = "grace_over" };
    }
}
