using WinTweaker.ViewModels;

namespace WinTweaker;

/// <summary>
/// 共享 ViewModel，保证配置导入后各页 UI 状态一致。
/// </summary>
internal static class ViewModelLocator
{
    private static GeneralViewModel? _general;
    private static UpdateViewModel? _update;
    private static Win11ViewModel? _win11;
    private static DangerViewModel? _danger;
    private static LogViewModel? _log;
    private static ProfileViewModel? _profile;

    public static GeneralViewModel General => _general ??= new GeneralViewModel();
    public static UpdateViewModel Update => _update ??= new UpdateViewModel();
    public static Win11ViewModel Win11 => _win11 ??= new Win11ViewModel();
    public static DangerViewModel Danger => _danger ??= new DangerViewModel();
    public static LogViewModel Log => _log ??= new LogViewModel();
    public static ProfileViewModel Profile => _profile ??= new ProfileViewModel();
}
