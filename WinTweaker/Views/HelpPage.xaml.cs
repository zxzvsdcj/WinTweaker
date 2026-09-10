using System.Windows.Controls;

namespace WinTweaker.Views;

public partial class HelpPage : Page
{
    public HelpPage()
    {
        InitializeComponent();
        DataContext = ViewModelLocator.Profile;
    }
}
