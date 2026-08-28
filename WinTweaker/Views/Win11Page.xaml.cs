using System.Windows.Controls;

namespace WinTweaker.Views;

public partial class Win11Page : Page
{
    public Win11Page()
    {
        InitializeComponent();
        DataContext = new ViewModels.Win11ViewModel();
    }
}
