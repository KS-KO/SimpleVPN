using SimpleVPN.Core.Models;

namespace SimpleVPN.Mobile.ViewModels;

public sealed class ConnectionOptionItemViewModel
{
    public ConnectionOptionItemViewModel(VpnConnectionOption option)
    {
        Option = option;
    }

    public VpnConnectionOption Option { get; }
    public string DisplayName => Option.DisplayName;
    public string Description => Option.Description;
}
