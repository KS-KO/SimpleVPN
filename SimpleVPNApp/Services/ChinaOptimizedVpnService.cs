using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SimpleVPNApp.Models;

namespace SimpleVPNApp.Services;

/// <summary>
/// 중국 환경용 우회 모드에서 설치형 Outline Client 대신 휴대용 sing-box 엔진을 직접 실행합니다.
/// </summary>
public sealed class ChinaOptimizedVpnService : IVpnService
{
    private const string EngineFolderName = "sing-box";
    private readonly string _accessKeyOrEndpoint;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _runtimeDirectory;
    private readonly string _configPath;
    private readonly string _logPath;
    private Process? _engineProcess;
    private bool _disposed;

    public ChinaOptimizedVpnService(string accessKeyOrEndpoint)
    {
        _accessKeyOrEndpoint = accessKeyOrEndpoint?.Trim() ?? string.Empty;

        _runtimeDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SimpleVPN",
            EngineFolderName);
        _configPath = Path.Combine(_runtimeDirectory, "config.json");
        _logPath = Path.Combine(_runtimeDirectory, "sing-box.log");
    }

    public bool IsConnected { get; private set; }
    public event Action<string>? StatusChanged;

    public async Task ConnectAsync(VpnServer server)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(_accessKeyOrEndpoint))
            {
                throw new InvalidOperationException("China Mode를 사용하려면 Outline Access Key를 입력해 주세요.");
            }

            PublishStatus("China Mode 설정 확인 중...");

            var accessKey = ParseAccessKey(_accessKeyOrEndpoint);
            var enginePath = ResolveEnginePath();

            Directory.CreateDirectory(_runtimeDirectory);
            await File.WriteAllTextAsync(_configPath, BuildConfig(accessKey), Encoding.UTF8).ConfigureAwait(false);

            if (_engineProcess is { HasExited: false })
            {
                PublishStatus("기존 China Mode 엔진을 정리 중...");
                await StopEngineAsync().ConfigureAwait(false);
            }

            PublishStatus("portable sing-box 엔진 시작 중...");
            _engineProcess = StartEngine(enginePath);
            await Task.Delay(1800).ConfigureAwait(false);

            if (_engineProcess.HasExited)
            {
                var exitCode = _engineProcess.ExitCode;
                var tail = await TryReadLogTailAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"China Mode 엔진이 시작 직후 종료되었습니다. ExitCode={exitCode}. {tail}".Trim());
            }

            IsConnected = true;
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
            PublishStatus("China Mode 연결 해제 완료");
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            DisconnectAsync().GetAwaiter().GetResult();
        }
        catch
        {
        }

        _gate.Dispose();
        _disposed = true;
    }

    private AccessKeyConfig ParseAccessKey(string input)
    {
        if (input.StartsWith("ssconf://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("portable China Mode는 현재 `ss://` 형식의 Outline Access Key만 지원합니다.");
        }

        if (!input.StartsWith("ss://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("China Mode는 `ss://` 형식의 Outline Access Key가 필요합니다.");
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
            throw new InvalidOperationException("Outline Access Key 형식이 올바르지 않습니다.");
        }

        var userInfoPart = withoutScheme[..atIndex];
        var hostPart = withoutScheme[(atIndex + 1)..];
        var credentials = DecodeUserInfo(userInfoPart);

        var separator = credentials.IndexOf(':');
        if (separator <= 0 || separator == credentials.Length - 1)
        {
            throw new InvalidOperationException("Outline Access Key의 인증 정보가 올바르지 않습니다.");
        }

        var colonIndex = hostPart.LastIndexOf(':');
        if (colonIndex <= 0 || colonIndex == hostPart.Length - 1)
        {
            throw new InvalidOperationException("Outline Access Key의 서버 주소가 올바르지 않습니다.");
        }

        var server = hostPart[..colonIndex];
        var portText = hostPart[(colonIndex + 1)..];
        if (!int.TryParse(portText, out var port))
        {
            throw new InvalidOperationException("Outline Access Key의 포트 정보가 올바르지 않습니다.");
        }

        return new AccessKeyConfig(
            server.Trim(),
            port,
            credentials[..separator].Trim(),
            credentials[(separator + 1)..].Trim());
    }

    private static string DecodeUserInfo(string userInfoPart)
    {
        if (userInfoPart.Contains(':'))
        {
            return Uri.UnescapeDataString(userInfoPart);
        }

        var normalized = userInfoPart
            .Replace('-', '+')
            .Replace('_', '/');

        var remainder = normalized.Length % 4;
        if (remainder > 0)
        {
            normalized = normalized.PadRight(normalized.Length + (4 - remainder), '=');
        }

        var bytes = Convert.FromBase64String(normalized);
        return Encoding.UTF8.GetString(bytes);
    }

    private string ResolveEnginePath()
    {
        var appBase = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(appBase, "Runtime", EngineFolderName, "sing-box.exe"),
            Path.Combine(appBase, EngineFolderName, "sing-box.exe"),
            Path.Combine(Directory.GetParent(appBase)?.FullName ?? appBase, "Runtime", EngineFolderName, "sing-box.exe"),
            Path.Combine(Directory.GetParent(appBase)?.Parent?.FullName ?? appBase, "Runtime", EngineFolderName, "sing-box.exe")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            "portable China Mode 엔진을 찾지 못했습니다. `SimpleVPNApp\\Runtime\\sing-box\\sing-box.exe`와 필요한 DLL을 배치해 주세요.");
    }

    private string BuildConfig(AccessKeyConfig accessKey)
    {
        var config = new
        {
            log = new
            {
                disabled = false,
                level = "info",
                output = _logPath
            },
            inbounds = new object[]
            {
                new
                {
                    type = "mixed",
                    tag = "mixed-in",
                    listen = "127.0.0.1",
                    listen_port = 2080,
                    set_system_proxy = true
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
                    method = accessKey.Method,
                    password = accessKey.Password
                },
                new
                {
                    type = "direct",
                    tag = "direct"
                }
            },
            route = new
            {
                auto_detect_interface = true,
                final = "ss-out"
            }
        };

        return JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
    }

    private Process StartEngine(string enginePath)
    {
        if (File.Exists(_logPath))
        {
            File.Delete(_logPath);
        }

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
        _ = ConsumeStreamAsync(process.StandardOutput);
        _ = ConsumeStreamAsync(process.StandardError);
        return process;
    }

    private async Task StopEngineAsync()
    {
        if (_engineProcess == null)
        {
            return;
        }

        try
        {
            if (!_engineProcess.HasExited)
            {
                PublishStatus("China Mode 엔진 종료 중...");
                _engineProcess.Kill(entireProcessTree: true);
                await _engineProcess.WaitForExitAsync().ConfigureAwait(false);
            }
        }
        catch
        {
        }
        finally
        {
            _engineProcess.Dispose();
            _engineProcess = null;
        }
    }

    private async Task ConsumeStreamAsync(StreamReader reader)
    {
        try
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    PublishStatus(line);
                }
            }
        }
        catch
        {
        }
    }

    private async Task<string> TryReadLogTailAsync()
    {
        try
        {
            if (!File.Exists(_logPath))
            {
                return string.Empty;
            }

            var content = await File.ReadAllTextAsync(_logPath).ConfigureAwait(false);
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
            {
                return string.Empty;
            }

            return $"최근 로그: {lines[^1].Trim()}";
        }
        catch
        {
            return string.Empty;
        }
    }

    private void PublishStatus(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            StatusChanged?.Invoke(message.Trim());
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed record AccessKeyConfig(string Server, int Port, string Method, string Password);
}
