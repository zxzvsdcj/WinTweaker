using System.IO;
using System.Text.Json;

namespace WinTweaker.License;

public sealed class LicenseConfig
{
    public string AppId { get; set; } = "";
    public string AppSecret { get; set; } = "";
    public string BitableToken { get; set; } = "";
    public string TableId { get; set; } = "";
    public string ProductCode { get; set; } = "win-tweaker";

    public string CacheMaterial() => $"{AppId}|{AppSecret}|{BitableToken}|{TableId}";

    public static LicenseConfig Load(string? path = null)
    {
        if (!string.IsNullOrWhiteSpace(path))
            return ReadFile(path);
        if (LicenseEmbed.HasCredentials)
            return LicenseEmbed.ToConfig();
        var env = Environment.GetEnvironmentVariable("LICENSE_CONFIG");
        if (!string.IsNullOrWhiteSpace(env))
            return ReadFile(env);
        throw new FileNotFoundException("构建未嵌入授权凭证，请联系管理员获取正式版本");
    }

    private static LicenseConfig ReadFile(string path)
    {
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var cfg = new LicenseConfig
        {
            AppId = GetString(root, "app_id"),
            AppSecret = GetString(root, "app_secret"),
            BitableToken = GetString(root, "bitable_token"),
            TableId = GetString(root, "table_id"),
            ProductCode = GetString(root, "product_code"),
        };
        if (string.IsNullOrWhiteSpace(cfg.ProductCode))
            cfg.ProductCode = "win-tweaker";
        foreach (var key in new[] { "app_id", "app_secret", "bitable_token", "table_id" })
        {
            var val = GetString(root, key);
            if (string.IsNullOrWhiteSpace(val) || val.StartsWith("飞书", StringComparison.Ordinal))
                throw new InvalidOperationException($"授权凭证字段无效: {key}");
        }
        return cfg;
    }

    private static string GetString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var el) ? (el.GetString() ?? "") : "";
    }
}
