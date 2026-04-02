using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimpleVPN.Core.Models;
using SimpleVPN.Core.Protocols;

namespace SimpleVPNApp.Services;

/// <summary>
/// China Mode에서 portable sing-box 엔진을 직접 실행합니다.
/// </summary>
public sealed class ChinaOptimizedVpnService : IVpnService
{
    private const string EngineFolderName = "sing-box";
    private const int PreferredListenPort = 2080;

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
        SingBoxConfigBuilder.ValidatePayload(connectionPayload);
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
            SingBoxConfigBuilder.ValidatePayload(_connectionPayload);

            PublishStatus("China Mode 엔진 점검 중...");
            await _provisioningService.EnsureEngineReadyAsync(PublishStatus).ConfigureAwait(false);
            var enginePath = ResolveEnginePath();

            if (!File.Exists(enginePath))
            {
                throw new FileNotFoundException("China Mode 엔진을 설치하지 못했습니다. 네트워크 연결을 확인해 주세요.");
            }

            Directory.CreateDirectory(_runtimeDirectory);
            var configJson = SingBoxConfigBuilder.BuildConfigJson(
                _connectionPayload,
                _logPath,
                GetFreePort(PreferredListenPort),
                setSystemProxy: true);

            await File.WriteAllTextAsync(_configPath, configJson, new UTF8Encoding(false)).ConfigureAwait(false);

            if (_engineProcess is { HasExited: false })
            {
                PublishStatus("기존 China Mode 엔진 정리 중...");
                await StopEngineAsync().ConfigureAwait(false);
            }

            PublishStatus("방화벽 규칙 확인 중...");
            EnsureFirewallRule(enginePath);

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
            PublishStatus("China Mode 연결 엔진이 시작되었습니다. 프록시 적용과 공인 IP 변경을 확인해 주세요.");
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
            RemoveFirewallRule();
            ResetProxySettings();
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

    private string ResolveEnginePath() => _provisioningService.GetEnginePath();

    private void EnsureFirewallRule(string enginePath)
    {
        try
        {
            RemoveFirewallRule();
            const string ruleName = "SimpleVPN - China Mode Engine (sing-box)";
            RunNetsh($"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow program=\"{enginePath}\" enable=yes");
            RunNetsh($"advfirewall firewall add rule name=\"{ruleName}\" dir=out action=allow program=\"{enginePath}\" enable=yes");
            PublishStatus("방화벽 허용 규칙을 적용했습니다.");
        }
        catch (Exception ex)
        {
            PublishStatus($"방화벽 설정 경고: {ex.Message}");
        }
    }

    private void RemoveFirewallRule()
    {
        try
        {
            const string ruleName = "SimpleVPN - China Mode Engine (sing-box)";
            RunNetsh($"advfirewall firewall delete rule name=\"{ruleName}\"");
        }
        catch
        {
        }
    }

    [DllImport("wininet.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

    private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
    private const int INTERNET_OPTION_REFRESH = 37;

    private void ResetProxySettings()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true);
            if (key == null)
            {
                return;
            }

            key.SetValue("ProxyEnable", 0, Microsoft.Win32.RegistryValueKind.DWord);

            try
            {
                key.DeleteValue("ProxyServer", false);
            }
            catch
            {
            }

            try
            {
                key.DeleteValue("ProxyOverride", false);
            }
            catch
            {
            }

            RefreshSystemProxy();
        }
        catch
        {
        }
    }

    private static void RefreshSystemProxy()
    {
        InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
        InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
    }

    private static void RunNetsh(string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        process.Start();
        process.WaitForExit();
    }

    private Process StartEngineWithCapture(string enginePath, StringBuilder logCapture)
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
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var trimmed = line.Trim();
                PublishStatus(trimmed);
                if (capture.Length < 1000)
                {
                    capture.AppendLine(trimmed);
                }
            }
        }
        catch
        {
        }
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

    private async Task<string> TryReadLogTailAsync()
    {
        try
        {
            if (!File.Exists(_logPath))
            {
                return string.Empty;
            }

            using var fs = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            var content = await reader.ReadToEndAsync().ConfigureAwait(false);
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
            {
                return string.Empty;
            }

            for (var i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i].Trim();
                if (line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("FATAL", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("panic", StringComparison.OrdinalIgnoreCase))
                {
                    return $"원인: {line}";
                }
            }

            return $"최근 로그: {lines[^1].Trim()}";
        }
        catch (Exception ex)
        {
            return $"(로그 읽기 실패: {ex.Message})";
        }
    }

    private void PublishStatus(string? message)
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

    private static int GetFreePort(int startPort)
    {
        for (var port = startPort; port < startPort + 100; port++)
        {
            try
            {
                using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
                listener.Start();
                return port;
            }
            catch
            {
            }
        }

        return startPort;
    }
}
