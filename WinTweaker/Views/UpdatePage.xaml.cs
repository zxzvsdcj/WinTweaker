using System.Windows.Controls;
using WinTweaker.ViewModels;

namespace WinTweaker.Views;

public partial class UpdatePage : Page
{
    public UpdatePage()
    {
        InitializeComponent();
        DataContext = new UpdateViewModel();
    }
}
