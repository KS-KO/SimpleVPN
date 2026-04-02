using SimpleVPN.Core.Models;

namespace SimpleVPN.Mobile.ViewModels;

public sealed class ServerItemViewModel
{
    public ServerItemViewModel(VpnServer server)
    {
        Server = server;
    }

    public VpnServer Server { get; }
    public string Title => $"{Server.CountryShort} · {Server.HostName}";
    public string Subtitle => $"{Server.CountryLong} · {Server.IP}";
    public string LatencyText => Server.Ping > 0 ? $"{Server.Ping} ms" : "-";
}
