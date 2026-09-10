namespace WinTweaker.License;

/// <summary>
/// 全局授权门闩：未通过验证时核心优化功能拒绝执行。
/// </summary>
public static class LicenseGate
{
    private static int _licensed;

    public static bool IsLicensed => Volatile.Read(ref _licensed) != 0;

    public static LicenseCheckResult? LastResult { get; private set; }

    public static void SetResult(LicenseCheckResult result)
    {
        LastResult = result;
        Volatile.Write(ref _licensed, result.Ok ? 1 : 0);
    }

    /// <summary>
    /// 核心功能执行前检查；未授权时返回 false 并给出提示文案。
    /// </summary>
    public static bool EnsureLicensed(out string message)
    {
        if (IsLicensed)
        {
            message = "";
            return true;
        }

        var code = LastResult?.MachineCode ?? MachineCode.Generate();
        message = LastResult?.Message
            ?? "本机尚未激活，请将机器码发送给卖家完成授权";
        message = $"{message}\n机器码：{code}";
        return false;
    }
}
