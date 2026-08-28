using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using WinTweaker.Models;
using WinTweaker.Services;

namespace WinTweaker.ViewModels;

/// <summary>
/// 日志页 ViewModel
/// </summary>
public sealed class LogViewModel : ViewModelBase
{
    private readonly LogService _logService = LogService.Instance;

    public ObservableCollection<LogEntry> Logs => _logService.Logs;

    public ICommand ClearLogsCommand { get; }
    public ICommand CopyLogsCommand { get; }

    public LogViewModel()
    {
        ClearLogsCommand = new RelayCommand(ClearLogs);
        CopyLogsCommand = new RelayCommand(CopyLogs);
    }

    private void ClearLogs()
    {
        _logService.Clear();
    }

    private void CopyLogs()
    {
        string allText = _logService.GetAllText();
        if (!string.IsNullOrEmpty(allText))
        {
            Clipboard.SetText(allText);
            _logService.Info("日志已复制到剪贴板");
        }
    }
}
