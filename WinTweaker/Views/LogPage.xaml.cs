using System.Windows.Controls;

namespace WinTweaker.Views;

public partial class LogPage : Page
{
    public LogPage()
    {
        InitializeComponent();
        DataContext = new ViewModels.LogViewModel();
    }
}
