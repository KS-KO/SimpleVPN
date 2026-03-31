using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SimpleVPNApp.Models;

namespace SimpleVPNApp.Services;

/// <summary>
/// China Mode에서 portable sing-box 엔진을 직접 실행합니다.
/// </summary>
public sealed class ChinaOptimizedVpnService : IVpnService
{
    private const string OutlineProfileType = "outline";
    private const string VlessRealityProfileType = "vless-reality";
    private const string TrojanProfileType = "trojan";
    private const string EngineFolderName = "sing-box";

    private readonly string _connectionPayload;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _runtimeDirectory;
    private readonly string _configPath;
    private readonly string _logPath;
    private readonly EngineProvisioningService _provisioningService = new();
    private Process? _engineProcess;
    private bool _disposed;
    private DateTime? _startTime;
    private long _lastReceived;
    private long _lastSent;

    public ChinaOptimizedVpnService(string connectionPayload)
    {
        _connectionPayload = connectionPayload?.Trim() ?? string.Empty;

        _runtimeDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SimpleVPN",
            EngineFolderName);
        _configPath = Path.Combine(_runtimeDirectory, "config.json");
        _logPath = Path.Combine(_runtimeDirectory, "sing-box.log");
    }

    public bool IsConnected { get; private set; }
    public event Action<string>? StatusChanged;

    public static void ValidateConnectionPayload(string connectionPayload)
    {
        var validator = new ChinaOptimizedVpnService(connectionPayload);
        var profile = validator.ParseProfile(connectionPayload);
        _ = validator.BuildConfig(profile);
    }

    public async Task ConnectAsync(VpnServer server)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(_connectionPayload))
            {
                throw new InvalidOperationException("China Mode를 사용하려면 프로필 설정을 입력해 주세요.");
            }

            PublishStatus("China Mode 설정 확인 중...");
            var profile = ParseProfile(_connectionPayload);

            PublishStatus("China Mode 엔진 점검 중...");
            await _provisioningService.EnsureEngineReadyAsync(msg => PublishStatus(msg)).ConfigureAwait(false);
            var enginePath = ResolveEnginePath();

            if (!File.Exists(enginePath))
            {
                throw new FileNotFoundException("China Mode 엔진을 설치하지 못했습니다. 네트워크 연결을 확인해 주세요.");
            }

            Directory.CreateDirectory(_runtimeDirectory);
            var utf8WithoutBom = new System.Text.UTF8Encoding(false);
            await File.WriteAllTextAsync(_configPath, BuildConfig(profile), utf8WithoutBom).ConfigureAwait(false);

            if (_engineProcess is { HasExited: false })
            {
                PublishStatus("기존 China Mode 엔진을 정리 중...");
                await StopEngineAsync().ConfigureAwait(false);
            }

            PublishStatus("방화벽 규칙 확인 중...");
            EnsureFirewallRule(enginePath);

            if (!File.Exists(enginePath)) throw new FileNotFoundException("엔진 파일이 작업 디렉토리에 없습니다.");

            PublishStatus("portable sing-box 엔진 시작 중...");
            var streamLog = new StringBuilder();
            _engineProcess = StartEngineWithCapture(enginePath, streamLog);
            
            await Task.Delay(2000).ConfigureAwait(false);

            if (_engineProcess.HasExited)
            {
                var exitCode = _engineProcess.ExitCode;
                var fileLog = await TryReadLogTailAsync().ConfigureAwait(false);
                var finalLog = !string.IsNullOrWhiteSpace(fileLog) ? fileLog : streamLog.ToString().Trim();
                
                throw new InvalidOperationException($"China Mode 엔진이 즉시 종료되었습니다. ExitCode={exitCode}. {finalLog}".Trim());
            }

            IsConnected = true;
            _startTime = DateTime.Now;
            _lastReceived = 0;
            _lastSent = 0;
            PublishStatus("China Mode 연결 엔진이 시작되었습니다. 시스템 프록시 적용과 공인 IP 변화를 확인하세요.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await StopEngineAsync().ConfigureAwait(false);
            IsConnected = false;
            _startTime = null;
            PublishStatus("China Mode 연결 해제 완료");
        }
        finally
        {
            _gate.Release();
        }
    }

    public VpnStatistics GetStatistics()
    {
        return Helpers.StatisticsHelper.GetProcessStatistics(_engineProcess, _startTime, _lastReceived, _lastSent);
    }

    public void Dispose()
    {
        if (_disposed) return;
        try { DisconnectAsync().GetAwaiter().GetResult(); } catch { }
        _gate.Dispose();
        _disposed = true;
    }

    private ChinaModeConnectionProfile ParseProfile(string input)
    {
        if (input.StartsWith("ss://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("ssconf://", StringComparison.OrdinalIgnoreCase))
        {
            return new ChinaModeConnectionProfile
            {
                ProfileType = OutlineProfileType,
                AccessKey = input
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

    private AccessKeyConfig ParseAccessKey(string input)
    {
        if (input.StartsWith("ssconf://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("portable China Mode는 현재 `ss://` 형식의 Outline Access Key만 지원합니다.");
        }

        if (!input.StartsWith("ss://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Outline 프로필은 `ss://` 형식의 Access Key가 필요합니다.");
        }

        var withoutScheme = input["ss://".Length..];
        var fragmentIndex = withoutScheme.IndexOf('#');
        if (fragmentIndex >= 0) withoutScheme = withoutScheme[..fragmentIndex];

        var queryIndex = withoutScheme.IndexOf('?');
        if (queryIndex >= 0) withoutScheme = withoutScheme[..queryIndex];

        var atIndex = withoutScheme.LastIndexOf('@');
        if (atIndex < 0)
        {
            var decoded = DecodeUserInfo(withoutScheme);
            return ParseSshFullString(decoded);
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
                throw new InvalidOperationException("Outline Access Key의 인증 정보가 올바르지 않습니다.");
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

    private AccessKeyConfig ParseSshFullString(string decoded)
    {
        var atIndex = decoded.LastIndexOf('@');
        if (atIndex < 0) throw new InvalidOperationException("내부 인증 정보에 @ 기호가 없습니다.");

        var authPart = decoded[..atIndex];
        var hostPart = decoded[(atIndex + 1)..];

        var authSep = authPart.IndexOf(':');
        if (authSep <= 0) throw new InvalidOperationException("인증 정보(method:password) 형식이 잘못되었습니다.");

        var hostSep = hostPart.LastIndexOf(':');
        if (hostSep <= 0) throw new InvalidOperationException($"서버 주소({hostPart}) 형식이 잘못되었습니다.");

        var portText = hostPart[(hostSep + 1)..].Trim();
        if (!int.TryParse(portText, out var port))
            throw new InvalidOperationException($"포트 정보({portText})가 올바르지 않습니다.");

        return new AccessKeyConfig(
            hostPart[..hostSep].Trim(),
            port,
            authPart[..authSep].Trim(),
            authPart[(authSep + 1)..].Trim());
    }

    private static string DecodeUserInfo(string userInfoPart)
    {
        if (userInfoPart.Contains(':')) return Uri.UnescapeDataString(userInfoPart);

        var normalized = userInfoPart.Replace('-', '+').Replace('_', '/');
        var remainder = normalized.Length % 4;
        if (remainder > 0) normalized = normalized.PadRight(normalized.Length + (4 - remainder), '=');

        var bytes = Convert.FromBase64String(normalized);
        return Encoding.UTF8.GetString(bytes);
    }

    private string ResolveEnginePath()
    {
        return _provisioningService.GetEnginePath();
    }

    private string BuildConfig(ChinaModeConnectionProfile profile)
    {
        var config = profile.ProfileType switch
        {
            OutlineProfileType => BuildOutlineConfig(ParseAccessKey(profile.AccessKey)),
            VlessRealityProfileType => BuildVlessRealityConfig(profile),
            TrojanProfileType => BuildTrojanConfig(profile),
            _ => throw new InvalidOperationException($"지원하지 않는 China Profile입니다: {profile.ProfileType}")
        };

        return JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
    }

    private object BuildOutlineConfig(AccessKeyConfig accessKey)
    {
        return new
        {
            log = new { disabled = false, level = "info", output = _logPath },
            inbounds = new object[]
            {
                new { type = "mixed", tag = "mixed-in", listen = "127.0.0.1", listen_port = GetFreePort(2080), set_system_proxy = true }
            },
            outbounds = new object[]
            {
                new { type = "shadowsocks", tag = "ss-out", server = accessKey.Server, server_port = accessKey.Port, method = NormalizeSshMethod(accessKey.Method), password = accessKey.Password },
                new { type = "direct", tag = "direct" }
            },
            route = new { auto_detect_interface = true, final = "ss-out" }
        };
    }

    private object BuildVlessRealityConfig(ChinaModeConnectionProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Server)) throw new InvalidOperationException("VLESS REALITY 서버 주소를 입력해 주세요.");
        if (profile.Port <= 0 || profile.Port > 65535) throw new InvalidOperationException("VLESS REALITY 포트가 올바르지 않습니다.");
        if (string.IsNullOrWhiteSpace(profile.Uuid)) throw new InvalidOperationException("VLESS REALITY UUID를 입력해 주세요.");
        if (string.IsNullOrWhiteSpace(profile.PublicKey)) throw new InvalidOperationException("VLESS REALITY Public Key를 입력해 주세요.");
        if (string.IsNullOrWhiteSpace(profile.ServerName)) throw new InvalidOperationException("VLESS REALITY Server Name(SNI)을 입력해 주세요.");

        return new
        {
            log = new { disabled = false, level = "info", output = _logPath },
            inbounds = new object[]
            {
                new { type = "mixed", tag = "mixed-in", listen = "127.0.0.1", listen_port = GetFreePort(2080), set_system_proxy = true }
            },
            outbounds = new object[]
            {
                new
                {
                    type = "vless", tag = "vless-reality-out", server = profile.Server, server_port = profile.Port, uuid = profile.Uuid, packet_encoding = "xudp",
                    tls = new { enabled = true, server_name = profile.ServerName, utls = new { enabled = true, fingerprint = profile.Fingerprint }, reality = new { enabled = true, public_key = profile.PublicKey, short_id = profile.ShortId } }
                },
                new { type = "direct", tag = "direct" }
            },
            route = new { auto_detect_interface = true, final = "vless-reality-out" }
        };
    }

    private object BuildTrojanConfig(ChinaModeConnectionProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Server)) throw new InvalidOperationException("Trojan 서버 주소를 입력해 주세요.");
        if (profile.Port <= 0 || profile.Port > 65535) throw new InvalidOperationException("Trojan 포트가 올바르지 않습니다.");
        if (string.IsNullOrWhiteSpace(profile.Password)) throw new InvalidOperationException("Trojan 비밀번호를 입력해 주세요.");
        if (string.IsNullOrWhiteSpace(profile.ServerName)) throw new InvalidOperationException("Trojan Server Name(SNI)을 입력해 주세요.");

        return new
        {
            log = new { disabled = false, level = "info", output = _logPath },
            inbounds = new object[]
            {
                new { type = "mixed", tag = "mixed-in", listen = "127.0.0.1", listen_port = GetFreePort(2080), set_system_proxy = true }
            },
            outbounds = new object[]
            {
                new
                {
                    type = "trojan", tag = "trojan-out", server = profile.Server, server_port = profile.Port, password = profile.Password,
                    tls = new { enabled = true, server_name = profile.ServerName, utls = new { enabled = true, fingerprint = profile.Fingerprint } }
                },
                new { type = "direct", tag = "direct" }
            },
            route = new { auto_detect_interface = true, final = "trojan-out" }
        };
    }

    private void EnsureFirewallRule(string enginePath)
    {
        try
        {
            const string ruleName = "SimpleVPN - China Mode Engine (sing-box)";
            RunNetsh($"advfirewall firewall delete rule name=\"{ruleName}\"");
            RunNetsh($"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow program=\"{enginePath}\" enable=yes");
            RunNetsh($"advfirewall firewall add rule name=\"{ruleName}\" dir=out action=allow program=\"{enginePath}\" enable=yes");
            PublishStatus("방화벽 허용 규칙을 적용했습니다.");
        }
        catch (Exception ex) { PublishStatus($"방화벽 설정 경고: {ex.Message}"); }
    }

    private static void RunNetsh(string arguments)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo { FileName = "netsh", Arguments = arguments, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
        process.Start(); process.WaitForExit();
    }

    private Process StartEngineWithCapture(string enginePath, StringBuilder logCapture)
    {
        if (File.Exists(_logPath)) File.Delete(_logPath);
        var startInfo = new ProcessStartInfo
        {
            FileName = enginePath,
            Arguments = $"run -c \"{_configPath}\"",
            WorkingDirectory = Path.GetDirectoryName(enginePath) ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var process = new Process { StartInfo = startInfo };
        process.Start();
        _ = ConsumeStreamWithCaptureAsync(process.StandardOutput, logCapture);
        _ = ConsumeStreamWithCaptureAsync(process.StandardError, logCapture);
        return process;
    }

    private async Task ConsumeStreamWithCaptureAsync(StreamReader reader, StringBuilder capture)
    {
        try
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    var trimmed = line.Trim();
                    PublishStatus(trimmed);
                    if (capture.Length < 1000) capture.AppendLine(trimmed); // 최대 1000자만 캡처
                }
            }
        }
        catch { }
    }

    private Process StartEngine(string enginePath)
    {
        if (File.Exists(_logPath)) File.Delete(_logPath);
        var startInfo = new ProcessStartInfo { FileName = enginePath, Arguments = $"run -c \"{_configPath}\"", WorkingDirectory = Path.GetDirectoryName(enginePath) ?? AppContext.BaseDirectory, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        var process = new Process { StartInfo = startInfo };
        process.Start();
        _ = ConsumeStreamAsync(process.StandardOutput);
        _ = ConsumeStreamAsync(process.StandardError);
        return process;
    }

    private async Task StopEngineAsync()
    {
        if (_engineProcess == null) return;
        try { if (!_engineProcess.HasExited) { _engineProcess.Kill(entireProcessTree: true); await _engineProcess.WaitForExitAsync().ConfigureAwait(false); } } catch { }
        finally { _engineProcess.Dispose(); _engineProcess = null; }
    }

    private async Task ConsumeStreamAsync(StreamReader reader)
    {
        try { while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line) { if (!string.IsNullOrWhiteSpace(line)) PublishStatus(line); } } catch { }
    }

    private async Task<string> TryReadLogTailAsync()
    {
        try
        {
            if (!File.Exists(_logPath)) return string.Empty;
            using var fs = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            var content = await reader.ReadToEndAsync().ConfigureAwait(false);
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return string.Empty;
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i].Trim();
                if (line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) || line.Contains("FATAL", StringComparison.OrdinalIgnoreCase) || line.Contains("panic", StringComparison.OrdinalIgnoreCase)) return $"원인: {line}";
            }
            return $"최근 로그: {lines[^1].Trim()}";
        }
        catch (Exception ex) { return $"(로그 읽기 실패: {ex.Message})"; }
    }

    private void PublishStatus(string message) { if (!string.IsNullOrWhiteSpace(message)) StatusChanged?.Invoke(message.Trim()); }
    private void ThrowIfDisposed() { ObjectDisposedException.ThrowIf(_disposed, this); }

    private static int GetFreePort(int startPort)
    {
        for (int port = startPort; port < startPort + 100; port++)
        {
            try { using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port); listener.Start(); return port; } catch { }
        }
        return startPort;
    }

    private static string NormalizeSshMethod(string method)
    {
        if (string.IsNullOrWhiteSpace(method)) return "chacha20-ietf-poly1305";
        var normalized = method.ToLowerInvariant().Trim();
        return normalized switch { "chacha20-poly1305" => "chacha20-ietf-poly1305", "chacha20-ietf" => "chacha20-ietf-poly1305", _ => normalized };
    }

    private sealed record AccessKeyConfig(string Server, int Port, string Method, string Password);
}
