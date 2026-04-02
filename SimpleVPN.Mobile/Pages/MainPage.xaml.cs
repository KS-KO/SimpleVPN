using SimpleVPN.Mobile.ViewModels;

namespace SimpleVPN.Mobile.Pages;

public partial class MainPage : ContentPage
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
