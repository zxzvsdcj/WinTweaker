using WinTweaker.Services;

namespace WinTweaker.ViewModels;

/// <summary>
/// 更新管理 ViewModel —— Edge / Chrome 自动更新控制
/// </summary>
public sealed class UpdateViewModel : ViewModelBase
{
    private readonly UpdateService _update = UpdateService.Instance;
    private readonly LogService _log = LogService.Instance;

    private bool _isEdgeUpdateDisabled;
    private bool _isChromeUpdateDisabled;

    public bool IsEdgeUpdateDisabled
    {
        get => _isEdgeUpdateDisabled;
        set
        {
            if (SetProperty(ref _isEdgeUpdateDisabled, value))
            {
                if (value) _update.DisableEdgeUpdate();
                else _update.RestoreEdgeUpdate();
            }
        }
    }

    public bool IsChromeUpdateDisabled
    {
        get => _isChromeUpdateDisabled;
        set
        {
            if (SetProperty(ref _isChromeUpdateDisabled, value))
            {
                if (value) _update.DisableChromeUpdate();
                else _update.RestoreChromeUpdate();
            }
        }
    }

    public UpdateViewModel()
    {
        ScanCurrentState();
    }

    private void ScanCurrentState()
    {
        _isEdgeUpdateDisabled = _update.IsEdgeUpdateDisabled();
        OnPropertyChanged(nameof(IsEdgeUpdateDisabled));

        _isChromeUpdateDisabled = _update.IsChromeUpdateDisabled();
        OnPropertyChanged(nameof(IsChromeUpdateDisabled));
    }
}
