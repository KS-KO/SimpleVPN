using System.Text;
using System.Text.Json;
using SimpleVPN.Core.Models;

namespace SimpleVPN.Core.Protocols;

public static class SingBoxConfigBuilder
{
    private const string OutlineProfileType = "outline";
    private const string VlessRealityProfileType = "vless-reality";
    private const string TrojanProfileType = "trojan";

    public static void ValidatePayload(string connectionPayload)
    {
        _ = BuildConfigJson(connectionPayload, logPath: "sing-box.log", listenPort: 2080, setSystemProxy: false);
    }

    public static string BuildConfigJson(string connectionPayload, string logPath, int listenPort, bool setSystemProxy)
    {
        var profile = ParseProfile(connectionPayload);
        var config = profile.ProfileType switch
        {
            OutlineProfileType => BuildOutlineConfig(ParseAccessKey(profile.AccessKey), logPath, listenPort, setSystemProxy),
            VlessRealityProfileType => BuildVlessRealityConfig(profile, logPath, listenPort, setSystemProxy),
            TrojanProfileType => BuildTrojanConfig(profile, logPath, listenPort, setSystemProxy),
            _ => throw new InvalidOperationException($"지원하지 않는 China Profile입니다: {profile.ProfileType}")
        };

        return JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
    }

    public static ChinaModeConnectionProfile ParseProfile(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new InvalidOperationException("China Mode 설정을 입력해 주세요.");
        }

        if (input.StartsWith("ss://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("ssconf://", StringComparison.OrdinalIgnoreCase))
        {
            return new ChinaModeConnectionProfile
            {
                ProfileType = OutlineProfileType,
                AccessKey = input.Trim()
            };
        }

        try
        {
            var profile = JsonSerializer.Deserialize<ChinaModeConnectionProfile>(input);
            if (profile == null || string.IsNullOrWhiteSpace(profile.ProfileType))
            {
                throw new InvalidOperationException("China Mode 설정을 읽지 못했습니다.");
            }

            return new ChinaModeConnectionProfile
            {
                ProfileType = profile.ProfileType.Trim(),
                AccessKey = profile.AccessKey?.Trim() ?? string.Empty,
                Server = profile.Server?.Trim() ?? string.Empty,
                Port = profile.Port,
                Uuid = profile.Uuid?.Trim() ?? string.Empty,
                PublicKey = profile.PublicKey?.Trim() ?? string.Empty,
                ShortId = profile.ShortId?.Trim() ?? string.Empty,
                ServerName = profile.ServerName?.Trim() ?? string.Empty,
                Fingerprint = string.IsNullOrWhiteSpace(profile.Fingerprint) ? "chrome" : profile.Fingerprint.Trim(),
                Password = profile.Password?.Trim() ?? string.Empty
            };
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("China Mode 설정 형식이 올바르지 않습니다.");
        }
    }

    private static AccessKeyConfig ParseAccessKey(string input)
    {
        if (input.StartsWith("ssconf://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("현재는 `ss://` 형식의 Outline Access Key만 지원합니다.");
        }

        if (!input.StartsWith("ss://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Outline 프로필에는 `ss://` 형식의 Access Key가 필요합니다.");
        }

        var withoutScheme = input["ss://".Length..];
        var fragmentIndex = withoutScheme.IndexOf('#');
        if (fragmentIndex >= 0)
        {
            withoutScheme = withoutScheme[..fragmentIndex];
        }

        var queryIndex = withoutScheme.IndexOf('?');
        if (queryIndex >= 0)
        {
            withoutScheme = withoutScheme[..queryIndex];
        }

        var atIndex = withoutScheme.LastIndexOf('@');
        if (atIndex < 0)
        {
            return ParseShadowsocksFullString(DecodeUserInfo(withoutScheme));
        }

        var userInfoPart = withoutScheme[..atIndex];
        var hostPart = withoutScheme[(atIndex + 1)..];
        var credentials = DecodeUserInfo(userInfoPart);

        var separator = credentials.IndexOf(':');
        if (separator <= 0 || separator == credentials.Length - 1)
        {
            if (userInfoPart.Contains(':'))
            {
                credentials = userInfoPart;
                separator = credentials.IndexOf(':');
            }

            if (separator <= 0 || separator == credentials.Length - 1)
            {
                throw new InvalidOperationException("Outline Access Key의 인증 정보가 올바르지 않습니다.");
            }
        }

        var colonIndex = hostPart.LastIndexOf(':');
        if (colonIndex <= 0 || colonIndex == hostPart.Length - 1)
        {
            throw new InvalidOperationException($"Outline Access Key의 서버 주소 또는 포트 정보가 올바르지 않습니다. (Host: {hostPart})");
        }

        var server = hostPart[..colonIndex].Trim('[', ']', ' ');
        var portText = hostPart[(colonIndex + 1)..].Trim();

        if (!int.TryParse(portText, out var port))
        {
            var onlyDigits = new string(portText.TakeWhile(char.IsDigit).ToArray());
            if (string.IsNullOrEmpty(onlyDigits) || !int.TryParse(onlyDigits, out port))
            {
                throw new InvalidOperationException($"Outline Access Key의 포트 정보({portText})가 숫자 형식이 아닙니다.");
            }
        }

        return new AccessKeyConfig(
            server.Trim(),
            port,
            credentials[..separator].Trim(),
            credentials[(separator + 1)..].Trim());
    }

    private static AccessKeyConfig ParseShadowsocksFullString(string decoded)
    {
        var atIndex = decoded.LastIndexOf('@');
        if (atIndex < 0)
        {
            throw new InvalidOperationException("인증 정보에 @ 기호가 없습니다.");
        }

        var authPart = decoded[..atIndex];
        var hostPart = decoded[(atIndex + 1)..];

        var authSep = authPart.IndexOf(':');
        if (authSep <= 0)
        {
            throw new InvalidOperationException("인증 정보(method:password) 형식이 잘못되었습니다.");
        }

        var hostSep = hostPart.LastIndexOf(':');
        if (hostSep <= 0)
        {
            throw new InvalidOperationException($"서버 주소({hostPart}) 형식이 잘못되었습니다.");
        }

        var portText = hostPart[(hostSep + 1)..].Trim();
        if (!int.TryParse(portText, out var port))
        {
            throw new InvalidOperationException($"포트 정보({portText})가 올바르지 않습니다.");
        }

        return new AccessKeyConfig(
            hostPart[..hostSep].Trim(),
            port,
            authPart[..authSep].Trim(),
            authPart[(authSep + 1)..].Trim());
    }

    private static object BuildOutlineConfig(AccessKeyConfig accessKey, string logPath, int listenPort, bool setSystemProxy)
    {
        return new
        {
            log = new { disabled = false, level = "info", output = logPath },
            inbounds = new object[]
            {
                new
                {
                    type = "mixed",
                    tag = "mixed-in",
                    listen = "127.0.0.1",
                    listen_port = listenPort,
                    set_system_proxy = setSystemProxy
                }
            },
            outbounds = new object[]
            {
                new
                {
                    type = "shadowsocks",
                    tag = "ss-out",
                    server = accessKey.Server,
                    server_port = accessKey.Port,
                    method = NormalizeShadowsocksMethod(accessKey.Method),
                    password = accessKey.Password
                },
                new { type = "direct", tag = "direct" }
            },
            route = new { auto_detect_interface = true, final = "ss-out" }
        };
    }

    private static object BuildVlessRealityConfig(ChinaModeConnectionProfile profile, string logPath, int listenPort, bool setSystemProxy)
    {
        if (string.IsNullOrWhiteSpace(profile.Server))
        {
            throw new InvalidOperationException("VLESS REALITY 서버 주소를 입력해 주세요.");
        }

        if (profile.Port <= 0 || profile.Port > 65535)
        {
            throw new InvalidOperationException("VLESS REALITY 포트가 올바르지 않습니다.");
        }

        if (string.IsNullOrWhiteSpace(profile.Uuid))
        {
            throw new InvalidOperationException("VLESS REALITY UUID를 입력해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(profile.PublicKey))
        {
            throw new InvalidOperationException("VLESS REALITY Public Key를 입력해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(profile.ServerName))
        {
            throw new InvalidOperationException("VLESS REALITY Server Name(SNI)을 입력해 주세요.");
        }

        return new
        {
            log = new { disabled = false, level = "info", output = logPath },
            inbounds = new object[]
            {
                new
                {
                    type = "mixed",
                    tag = "mixed-in",
                    listen = "127.0.0.1",
                    listen_port = listenPort,
                    set_system_proxy = setSystemProxy
                }
            },
            outbounds = new object[]
            {
                new
                {
                    type = "vless",
                    tag = "vless-reality-out",
                    server = profile.Server,
                    server_port = profile.Port,
                    uuid = profile.Uuid,
                    packet_encoding = "xudp",
                    tls = new
                    {
                        enabled = true,
                        server_name = profile.ServerName,
                        utls = new { enabled = true, fingerprint = profile.Fingerprint },
                        reality = new { enabled = true, public_key = profile.PublicKey, short_id = profile.ShortId }
                    }
                },
                new { type = "direct", tag = "direct" }
            },
            route = new { auto_detect_interface = true, final = "vless-reality-out" }
        };
    }

    private static object BuildTrojanConfig(ChinaModeConnectionProfile profile, string logPath, int listenPort, bool setSystemProxy)
    {
        if (string.IsNullOrWhiteSpace(profile.Server))
        {
            throw new InvalidOperationException("Trojan 서버 주소를 입력해 주세요.");
        }

        if (profile.Port <= 0 || profile.Port > 65535)
        {
            throw new InvalidOperationException("Trojan 포트가 올바르지 않습니다.");
        }

        if (string.IsNullOrWhiteSpace(profile.Password))
        {
            throw new InvalidOperationException("Trojan 비밀번호를 입력해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(profile.ServerName))
        {
            throw new InvalidOperationException("Trojan Server Name(SNI)을 입력해 주세요.");
        }

        return new
        {
            log = new { disabled = false, level = "info", output = logPath },
            inbounds = new object[]
            {
                new
                {
                    type = "mixed",
                    tag = "mixed-in",
                    listen = "127.0.0.1",
                    listen_port = listenPort,
                    set_system_proxy = setSystemProxy
                }
            },
            outbounds = new object[]
            {
                new
                {
                    type = "trojan",
                    tag = "trojan-out",
                    server = profile.Server,
                    server_port = profile.Port,
                    password = profile.Password,
                    tls = new
                    {
                        enabled = true,
                        server_name = profile.ServerName,
                        utls = new { enabled = true, fingerprint = profile.Fingerprint }
                    }
                },
                new { type = "direct", tag = "direct" }
            },
            route = new { auto_detect_interface = true, final = "trojan-out" }
        };
    }

    private static string DecodeUserInfo(string userInfoPart)
    {
        if (userInfoPart.Contains(':'))
        {
            return Uri.UnescapeDataString(userInfoPart);
        }

        var normalized = userInfoPart.Replace('-', '+').Replace('_', '/');
        var remainder = normalized.Length % 4;
        if (remainder > 0)
        {
            normalized = normalized.PadRight(normalized.Length + (4 - remainder), '=');
        }

        var bytes = Convert.FromBase64String(normalized);
        return Encoding.UTF8.GetString(bytes);
    }

    private static string NormalizeShadowsocksMethod(string method)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            return "chacha20-ietf-poly1305";
        }

        var normalized = method.ToLowerInvariant().Trim();
        return normalized switch
        {
            "chacha20-poly1305" => "chacha20-ietf-poly1305",
            "chacha20-ietf" => "chacha20-ietf-poly1305",
            _ => normalized
        };
    }

    private sealed record AccessKeyConfig(string Server, int Port, string Method, string Password);
}
