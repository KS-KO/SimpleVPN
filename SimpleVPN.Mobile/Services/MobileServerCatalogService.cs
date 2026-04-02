using SimpleVPN.Core.Models;

namespace SimpleVPN.Mobile.Services;

public sealed class MobileServerCatalogService
{
    public Task<IReadOnlyList<VpnServer>> GetSampleServersAsync()
    {
        IReadOnlyList<VpnServer> servers =
        [
            new VpnServer
            {
                HostName = "Seoul Edge",
                CountryLong = "South Korea",
                CountryShort = "KR",
                IP = "203.0.113.10",
                Ping = 34,
                Speed = 125_000_000
            },
            new VpnServer
            {
                HostName = "Tokyo Relay",
                CountryLong = "Japan",
                CountryShort = "JP",
                IP = "203.0.113.20",
                Ping = 52,
                Speed = 98_000_000
            },
            new VpnServer
            {
                HostName = "Singapore Fast",
                CountryLong = "Singapore",
                CountryShort = "SG",
                IP = "203.0.113.30",
                Ping = 87,
                Speed = 140_000_000
            }
        ];

        return Task.FromResult(servers);
    }

    public IReadOnlyList<VpnConnectionOption> GetConnectionOptions()
    {
        return
        [
            new VpnConnectionOption
            {
                Key = "outline",
                DisplayName = "Outline",
                Description = "Shadowsocks access key 기반 연결"
            },
            new VpnConnectionOption
            {
                Key = "vless-reality",
                DisplayName = "VLESS REALITY",
                Description = "UUID, Public Key, SNI 기반 연결"
            },
            new VpnConnectionOption
            {
                Key = "trojan",
                DisplayName = "Trojan TLS",
                Description = "비밀번호와 TLS SNI 기반 연결"
            }
        ];
    }
}
