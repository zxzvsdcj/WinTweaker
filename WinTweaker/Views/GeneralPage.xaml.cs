using System.Windows.Controls;

namespace WinTweaker.Views;

public partial class GeneralPage : Page
{
    public GeneralPage()
    {
        InitializeComponent();
        DataContext = new ViewModels.GeneralViewModel();
    }
}
