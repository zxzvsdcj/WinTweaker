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

    public static LicenseConfig Load(string? path = null)
    {
        var candidates = new List<string>();
        var env = Environment.GetEnvironmentVariable("LICENSE_CONFIG");
        if (!string.IsNullOrEmpty(env))
            candidates.Add(env);

        if (!string.IsNullOrEmpty(path))
            candidates.Add(path);

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        candidates.Add(Path.Combine(baseDir, "license_config.json"));
        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "license_config.json"));
        candidates.Add(Path.Combine(baseDir, "..", "..", "..", "license_config.json"));

        // 仓库根 / WinTweaker 旁
        var projRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
        candidates.Add(Path.Combine(projRoot, "license_config.json"));
        candidates.Add(Path.Combine(projRoot, "WinTweaker", "license_config.json"));

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(candidate))
                continue;

            var json = File.ReadAllText(candidate);
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
                    throw new InvalidOperationException($"license_config.json 字段无效: {key}");
            }

            return cfg;
        }

        throw new FileNotFoundException(
            "未找到 license_config.json（可用 LICENSE_CONFIG 环境变量指定路径）");
    }

    private static string GetString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var el) ? (el.GetString() ?? "") : "";
    }
}
