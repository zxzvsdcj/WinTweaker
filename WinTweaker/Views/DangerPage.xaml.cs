using System.Windows.Controls;

namespace WinTweaker.Views;

public partial class DangerPage : Page
{
    public DangerPage()
    {
        InitializeComponent();
        DataContext = new ViewModels.DangerViewModel();
    }
}
