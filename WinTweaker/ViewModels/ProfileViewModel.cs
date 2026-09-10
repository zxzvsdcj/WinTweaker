using System.Linq;
using System.Windows;
using System.Windows.Input;
using WinTweaker.Services;

namespace WinTweaker.ViewModels;

public sealed class ProfileViewModel : ViewModelBase
{
    public ICommand ExportCommand { get; }
    public ICommand ImportCommand { get; }

    public ProfileViewModel()
    {
        ExportCommand = new RelayCommand(Export);
        ImportCommand = new RelayCommand(Import);
    }

    private void Export()
    {
        try
        {
            var path = ProfileService.ExportWithDialog();
            if (path == null)
                return;

            MessageBox.Show(
                $"方案已导出：\n{path}\n\n可将此 JSON 发给其他客户，在其电脑上「导入方案」即可复用。",
                "导出成功",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LogService.Instance.Error("导出方案失败：" + ex.Message);
            MessageBox.Show(ex.Message, "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Import()
    {
        try
        {
            var result = ProfileService.ImportWithDialog();
            if (result == null)
                return;

            var detail = result.ToSummary();
            if (result.UnknownKeys.Count > 0)
                detail += "\n\n未知键（已跳过，通常来自更新版本功能）：\n" + string.Join("\n", result.UnknownKeys.Take(12));

            MessageBox.Show(detail, "导入完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LogService.Instance.Error("导入方案失败：" + ex.Message);
            MessageBox.Show(ex.Message, "导入失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
