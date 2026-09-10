using System.Text.Json.Serialization;

namespace WinTweaker.Models;

/// <summary>
/// 可分发的优化方案（键值字典，增删功能时向前/向后兼容）。
/// </summary>
public sealed class TweakerProfileDocument
{
    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("product")]
    public string Product { get; set; } = "win-tweaker";

    [JsonPropertyName("exported_at")]
    public string ExportedAt { get; set; } = "";

    [JsonPropertyName("author")]
    public string Author { get; set; } = "微信号：zxzvsdcj";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "默认方案";

    /// <summary>
    /// 功能键 → 开关状态。未知键导入时跳过；缺失键保持现状。
    /// </summary>
    [JsonPropertyName("settings")]
    public Dictionary<string, bool> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ProfileApplyResult
{
    public int Applied { get; set; }
    public int Unchanged { get; set; }
    public int SkippedUnknown { get; set; }
    public int SkippedUnsupported { get; set; }
    public int Failed { get; set; }
    public List<string> UnknownKeys { get; } = new();
    public List<string> AppliedKeys { get; } = new();
    public List<string> FailedKeys { get; } = new();

    public string ToSummary()
    {
        return $"已应用 {Applied} 项，未变 {Unchanged} 项，跳过未知 {SkippedUnknown} 项，" +
               $"当前系统不支持 {SkippedUnsupported} 项，失败 {Failed} 项。";
    }
}
