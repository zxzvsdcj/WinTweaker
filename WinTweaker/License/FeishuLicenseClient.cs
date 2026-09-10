using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace WinTweaker.License;

/// <summary>
/// 飞书只读校验 + 时效判定（与 Python/Rust SDK 对齐）。
/// </summary>
public sealed class FeishuLicenseClient
{
    public const long PermanentExpireMs = 4102415999000L;
    private const long DayMs = 86_400_000L;
    private const string FeishuBase = "https://open.feishu.cn/open-apis";

    private readonly string _appId;
    private readonly string _appSecret;
    private readonly string _bitableToken;
    private readonly string _tableId;
    private readonly string _productCode;
    private readonly HttpClient _http;

    public FeishuLicenseClient(LicenseConfig config, HttpClient? http = null)
    {
        _appId = config.AppId;
        _appSecret = config.AppSecret;
        _bitableToken = config.BitableToken;
        _tableId = config.TableId;
        _productCode = string.IsNullOrWhiteSpace(config.ProductCode) ? "wuguard" : config.ProductCode;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    public sealed class LicenseInfo
    {
        public string LicenseType { get; set; } = "永久授权";
        public long ExpireTime { get; set; } = PermanentExpireMs;
        public long StartTime { get; set; }
        public long GrantDays { get; set; }
    }

    public sealed class OnlineStatus
    {
        public string Kind { get; init; } = "not_found"; // licensed | blacklisted | expired | not_found
        public LicenseInfo? Info { get; init; }
    }

    public static long RemainingDays(string licenseType, long expireTime)
    {
        if (licenseType == "永久授权" || expireTime >= PermanentExpireMs)
            return -1;
        var diff = expireTime - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (diff <= 0)
            return 0;
        return (diff + DayMs - 1) / DayMs;
    }

    public static string ExtractText(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return "";
            case JsonValueKind.String:
                return value.GetString() ?? "";
            case JsonValueKind.Number:
                return value.ToString();
            case JsonValueKind.Array:
                var parts = new StringBuilder();
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("text", out var t))
                        parts.Append(t.GetString());
                    else
                        parts.Append(ExtractText(item));
                }
                return parts.ToString();
            case JsonValueKind.Object:
                return value.TryGetProperty("text", out var text) ? ExtractText(text) : "";
            default:
                return value.ToString();
        }
    }

    public static long ExtractNumber(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            case JsonValueKind.False:
                return 0;
            case JsonValueKind.True:
                return 0;
            case JsonValueKind.Number:
                return value.TryGetInt64(out var n) ? n : (long)value.GetDouble();
            case JsonValueKind.Array:
                return value.GetArrayLength() > 0 ? ExtractNumber(value[0]) : 0;
            case JsonValueKind.Object:
                return value.TryGetProperty("text", out var text) ? ExtractNumber(text) : 0;
            case JsonValueKind.String:
                return long.TryParse(value.GetString(), out var parsed) ? parsed : 0;
            default:
                return long.TryParse(value.ToString(), out var fallback) ? fallback : 0;
        }
    }

    public async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new { app_id = _appId, app_secret = _appSecret });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync($"{FeishuBase}/auth/v3/tenant_access_token/internal", content, ct).ConfigureAwait(false);
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var root = doc.RootElement;
        if (root.TryGetProperty("code", out var code) && code.GetInt32() != 0)
            throw new InvalidOperationException($"飞书鉴权失败: {root.GetProperty("msg").GetString()}");
        if (!root.TryGetProperty("tenant_access_token", out var tokenEl))
            throw new InvalidOperationException("未返回 access_token");
        var token = tokenEl.GetString();
        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException("未返回 access_token");
        return token;
    }

    private async Task<List<JsonElement>> SearchAsync(string token, string machineCode, CancellationToken ct)
    {
        var url =
            $"{FeishuBase}/bitable/v1/apps/{_bitableToken}/tables/{_tableId}/records/search";
        var body = new
        {
            filter = new
            {
                conjunction = "and",
                conditions = new[]
                {
                    new
                    {
                        field_name = "machine_code",
                        @operator = "is",
                        value = new[] { machineCode },
                    },
                },
            },
            page_size = 100,
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var root = doc.RootElement;
        if (root.TryGetProperty("code", out var code) && code.GetInt32() != 0)
            throw new InvalidOperationException($"Search API 失败: {root.GetProperty("msg").GetString()}");

        var items = new List<JsonElement>();
        if (root.TryGetProperty("data", out var data) &&
            data.TryGetProperty("items", out var arr) &&
            arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
                items.Add(item.Clone());
        }
        return items;
    }

    private async Task<List<JsonElement>> ListFilterAsync(string token, string machineCode, CancellationToken ct)
    {
        var items = new List<JsonElement>();
        var pageToken = "";
        while (true)
        {
            var url =
                $"{FeishuBase}/bitable/v1/apps/{_bitableToken}/tables/{_tableId}/records?page_size=500";
            if (!string.IsNullOrEmpty(pageToken))
                url += $"&page_token={Uri.EscapeDataString(pageToken)}";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var root = doc.RootElement;
            if (root.TryGetProperty("code", out var code) && code.GetInt32() != 0)
                throw new InvalidOperationException($"拉取记录失败: {root.GetProperty("msg").GetString()}");

            var data = root.GetProperty("data");
            if (data.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    if (!item.TryGetProperty("fields", out var fields))
                        continue;
                    var mc = fields.TryGetProperty("machine_code", out var mcEl)
                        ? ExtractText(mcEl)
                        : "";
                    if (mc == machineCode)
                        items.Add(item.Clone());
                }
            }

            if (data.TryGetProperty("has_more", out var more) && more.GetBoolean())
            {
                pageToken = data.TryGetProperty("page_token", out var pt) ? (pt.GetString() ?? "") : "";
                if (string.IsNullOrEmpty(pageToken))
                    break;
            }
            else
            {
                break;
            }
        }

        return items;
    }

    public async Task<OnlineStatus> VerifyMachineAsync(string machineCode, CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct).ConfigureAwait(false);
        List<JsonElement> records;
        try
        {
            records = await SearchAsync(token, machineCode, ct).ConfigureAwait(false);
        }
        catch
        {
            records = await ListFilterAsync(token, machineCode, ct).ConfigureAwait(false);
        }

        JsonElement? matched = null;
        foreach (var record in records)
        {
            if (!record.TryGetProperty("fields", out var fields))
                continue;
            var mc = fields.TryGetProperty("machine_code", out var mcEl) ? ExtractText(mcEl) : "";
            if (mc != machineCode)
                continue;
            var recProduct = fields.TryGetProperty("product_code", out var pcEl)
                ? ExtractText(pcEl)
                : "";
            if (string.IsNullOrEmpty(recProduct))
                recProduct = "wuguard";
            if (recProduct != _productCode)
                continue;
            matched = record;
            break;
        }

        if (matched is null)
            return new OnlineStatus { Kind = "not_found" };

        var mfields = matched.Value.GetProperty("fields");
        var status = mfields.TryGetProperty("status", out var stEl) ? ExtractText(stEl) : "";
        if (status is "blacklist" or "blacklisted" or "拉黑")
            return new OnlineStatus { Kind = "blacklisted" };

        var licenseType = mfields.TryGetProperty("license_type", out var ltEl)
            ? ExtractText(ltEl)
            : "";
        if (string.IsNullOrEmpty(licenseType))
            licenseType = "永久授权";

        var expireTime = mfields.TryGetProperty("expire_time", out var etEl) ? ExtractNumber(etEl) : 0;
        var startTime = mfields.TryGetProperty("start_time", out var startEl) ? ExtractNumber(startEl) : 0;
        var grantDays = mfields.TryGetProperty("grant_days", out var gdEl) ? ExtractNumber(gdEl) : 0;

        if (licenseType == "永久授权" || expireTime == 0)
        {
            var info = new LicenseInfo
            {
                LicenseType = "永久授权",
                ExpireTime = PermanentExpireMs,
                StartTime = startTime,
                GrantDays = 0,
            };
            return new OnlineStatus { Kind = "licensed", Info = info };
        }

        if (expireTime <= 0)
            expireTime = PermanentExpireMs;

        var timed = new LicenseInfo
        {
            LicenseType = licenseType,
            ExpireTime = expireTime,
            StartTime = startTime,
            GrantDays = grantDays,
        };
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (nowMs > expireTime)
            return new OnlineStatus { Kind = "expired", Info = timed };
        return new OnlineStatus { Kind = "licensed", Info = timed };
    }
}
