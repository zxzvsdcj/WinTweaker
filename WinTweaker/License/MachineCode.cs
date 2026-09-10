using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace WinTweaker.License;

/// <summary>
/// 机器码：SHA256("WuGuard|" + MachineGuid + "|" + ComputerName) → 64 位小写 hex。
/// 与 WuGuard Python/Rust SDK 算法完全一致。
/// </summary>
public static class MachineCode
{
    public static string GetMachineGuid()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            var value = key?.GetValue("MachineGuid")?.ToString();
            return string.IsNullOrEmpty(value) ? "unknown-machine-guid" : value;
        }
        catch
        {
            return "unknown-machine-guid";
        }
    }

    public static string GetComputerName()
    {
        return Environment.GetEnvironmentVariable("COMPUTERNAME")
            ?? Environment.GetEnvironmentVariable("HOSTNAME")
            ?? "unknown-host";
    }

    public static string Generate()
    {
        var seed = $"WuGuard|{GetMachineGuid()}|{GetComputerName()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
