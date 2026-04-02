using System.Text.Json;
using SimpleVPN.Core.Models;

namespace SimpleVPN.Core.Protocols;

public static class ChinaModePayloadBuilder
{
    public static string BuildPayload(string? selectedProfileKey, ChinaModeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return selectedProfileKey switch
        {
            "vless-reality" => JsonSerializer.Serialize(new ChinaModeConnectionProfile
            {
                ProfileType = "vless-reality",
                Server = settings.VlessRealityServer,
                Port = ParsePort(settings.VlessRealityPort),
                Uuid = settings.VlessRealityUuid,
                PublicKey = settings.VlessRealityPublicKey,
                ShortId = settings.VlessRealityShortId,
                ServerName = settings.VlessRealityServerName,
                Fingerprint = settings.VlessRealityFingerprint
            }),
            "trojan" => JsonSerializer.Serialize(new ChinaModeConnectionProfile
            {
                ProfileType = "trojan",
                Server = settings.TrojanServer,
                Port = ParsePort(settings.TrojanPort),
                Password = settings.TrojanPassword,
                ServerName = settings.TrojanServerName,
                Fingerprint = settings.TrojanFingerprint
            }),
            _ => settings.OutlineAccessKey ?? string.Empty
        };
    }

    private static int ParsePort(string? value) =>
        int.TryParse(value, out var parsedPort) ? parsedPort : 0;
}
