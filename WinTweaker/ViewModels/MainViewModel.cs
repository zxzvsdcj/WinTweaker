using System.ComponentModel;
using System.Runtime.CompilerServices;
using WinTweaker.Models;
using WinTweaker.Services;

namespace WinTweaker.ViewModels;

/// <summary>
/// 主窗口 ViewModel —— 承载系统信息和全局状态
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    public string SystemVersionText { get; }
    public SystemCapabilities Capabilities { get; }

    public MainViewModel()
    {
        var sysInfo = SystemInfoService.Instance.Current;
        SystemVersionText = sysInfo.FullVersionString;
        Capabilities = new SystemCapabilities(sysInfo);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
