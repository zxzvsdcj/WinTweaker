using System.Text;

namespace WinTweaker.License;

/// <summary>
/// 编译期嵌入的只读凭证。XOR 只是混淆，不是加密。发布目录不应再出现 license_config.json。
/// </summary>
internal static class LicenseEmbed
{
    public static bool HasCredentials => AppId().Length > 0;

    public static LicenseConfig ToConfig()
    {
        return new LicenseConfig
        {
            AppId = AppId(),
            AppSecret = AppSecret(),
            BitableToken = BitableToken(),
            TableId = TableId(),
            ProductCode = string.IsNullOrWhiteSpace(LicenseEmbedData.ProductCode)
                ? "win-tweaker"
                : LicenseEmbedData.ProductCode,
        };
    }

    public static string CacheMaterial()
    {
        var cfg = ToConfig();
        return $"{cfg.AppId}|{cfg.AppSecret}|{cfg.BitableToken}|{cfg.TableId}";
    }

    private static string AppId() => Deobfuscate(LicenseEmbedData.AppIdObf);
    private static string AppSecret() => Deobfuscate(LicenseEmbedData.AppSecretObf);
    private static string BitableToken() => Deobfuscate(LicenseEmbedData.BitableTokenObf);
    private static string TableId() => Deobfuscate(LicenseEmbedData.TableIdObf);

    private static string Deobfuscate(byte[] data)
    {
        if (data.Length == 0) return "";
        var key = LicenseEmbedData.XorKey;
        var bytes = new byte[data.Length];
        for (var i = 0; i < data.Length; i++)
            bytes[i] = (byte)(data[i] ^ key[i % key.Length]);
        return Encoding.UTF8.GetString(bytes);
    }
}
