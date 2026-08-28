using System.Windows;
using System.Windows.Input;
using WinTweaker.Models;
using WinTweaker.Services;

namespace WinTweaker.ViewModels;

/// <summary>
/// 高危操作 ViewModel —— UAC/防火墙/Defender
/// 含二次确认和自动版本适配提示
/// </summary>
public sealed class DangerViewModel : ViewModelBase
{
    private readonly SystemSecurityService _security = SystemSecurityService.Instance;
    private readonly LogService _log = LogService.Instance;
    private readonly SystemCapabilities _caps;

    private bool _isUacDisabled;
    private bool _isFirewallDisabled;
    private bool _isDefenderSuppressed;

    public string DefenderWarningText { get; }

    public bool IsUacDisabled
    {
        get => _isUacDisabled;
        set
        {
            if (SetProperty(ref _isUacDisabled, value))
            {
                if (value)
                {
                    if (ConfirmDangerAction("关闭 UAC 将降低系统安全屏障，确定继续？"))
                        _security.DisableUac();
                    else
                        SetProperty(ref _isUacDisabled, false);
                }
                else
                {
                    _security.EnableUac();
                }
            }
        }
    }

    public bool IsFirewallDisabled
    {
        get => _isFirewallDisabled;
        set
        {
            if (SetProperty(ref _isFirewallDisabled, value))
            {
                if (value)
                {
                    if (ConfirmDangerAction("关闭防火墙将使系统暴露在网络威胁中，确定继续？"))
                        _security.DisableFirewall();
                    else
                        SetProperty(ref _isFirewallDisabled, false);
                }
                else
                {
                    _security.EnableFirewall();
                }
            }
        }
    }

    public bool IsDefenderSuppressed
    {
        get => _isDefenderSuppressed;
        set
        {
            if (SetProperty(ref _isDefenderSuppressed, value))
            {
                if (value)
                {
                    if (ConfirmDangerAction("压制 Defender 将移除实时防护，确定继续？"))
                        _security.SuppressDefender();
                    else
                        SetProperty(ref _isDefenderSuppressed, false);
                }
                else
                {
                    _security.RestoreDefender();
                }
            }
        }
    }

    public ICommand RollbackAllCommand { get; }

    public DangerViewModel()
    {
        _caps = new SystemCapabilities(SystemInfoService.Instance.Current);
        RollbackAllCommand = new RelayCommand(RollbackAll);

        // 生成 Defender 警告文本
        DefenderWarningText = _caps.GetDangerWarning("Defender")
            ?? "通过策略压制 Defender 实时防护";

        ScanCurrentState();
    }

    private void ScanCurrentState()
    {
        _isUacDisabled = !_security.IsUacEnabled();
        OnPropertyChanged(nameof(IsUacDisabled));

        _isFirewallDisabled = !_security.IsFirewallEnabled();
        OnPropertyChanged(nameof(IsFirewallDisabled));

        _isDefenderSuppressed = !_security.IsDefenderRealtimeEnabled();
        OnPropertyChanged(nameof(IsDefenderSuppressed));
    }

    private void RollbackAll()
    {
        _security.EnableUac();
        _security.EnableFirewall();
        _security.RestoreDefender();

        _isUacDisabled = false;
        _isFirewallDisabled = false;
        _isDefenderSuppressed = false;

        OnPropertyChanged(nameof(IsUacDisabled));
        OnPropertyChanged(nameof(IsFirewallDisabled));
        OnPropertyChanged(nameof(IsDefenderSuppressed));

        _log.Success("[高危回滚] 已恢复全部安全设置");
    }

    /// <summary>高危操作二次确认弹窗</summary>
    private static bool ConfirmDangerAction(string message)
    {
        var result = MessageBox.Show(
            message,
            "高危操作确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        return result == MessageBoxResult.Yes;
    }
}
