using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleVPN.Core.Models;
using SimpleVPN.Mobile.Services;

namespace SimpleVPN.Mobile.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IMobileVpnPlatformService _platformService;
    private readonly MobileServerCatalogService _catalogService;

    [ObservableProperty] private string _statusMessage = "Mobile VPN platform binding pending";
    [ObservableProperty] private string _connectButtonText = "Preview Mobile Flow";
    [ObservableProperty] private ConnectionOptionItemViewModel? _selectedConnectionOption;
    [ObservableProperty] private ServerItemViewModel? _selectedServer;
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<ConnectionOptionItemViewModel> ConnectionOptions { get; } = [];
    public ObservableCollection<ServerItemViewModel> Servers { get; } = [];

    public MainViewModel(
        IMobileVpnPlatformService platformService,
        MobileServerCatalogService catalogService)
    {
        _platformService = platformService;
        _catalogService = catalogService;

        LoadInitialData();
    }

    [RelayCommand]
    private async Task ToggleConnectionAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var summary = await _platformService.RequestPermissionSummaryAsync().ConfigureAwait(false);
            StatusMessage = summary;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void LoadInitialData()
    {
        ConnectionOptions.Clear();
        foreach (var option in _catalogService.GetConnectionOptions())
        {
            ConnectionOptions.Add(new ConnectionOptionItemViewModel(option));
        }

        SelectedConnectionOption = ConnectionOptions.FirstOrDefault();

        Servers.Clear();
        foreach (var server in _catalogService.GetSampleServersAsync().GetAwaiter().GetResult())
        {
            Servers.Add(new ServerItemViewModel(server));
        }

        SelectedServer = Servers.FirstOrDefault();
    }
}
