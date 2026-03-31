using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimpleVPNApp.Helpers;
using SimpleVPNApp.Models;

namespace SimpleVPNApp.Services;

/// <summary>
/// OpenVPN GUI를 통해 실제 VPN 터널 연결을 관리합니다.
/// </summary>
public sealed class OpenVpnService : IVpnService
{
    private const string ProfileName = "simplevpn_active";
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan DisconnectTimeout = TimeSpan.FromSeconds(15);
    private static readonly string GuiPath = @"C:\Program Files\OpenVPN\bin\openvpn-gui.exe";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _configDirectory;
    private readonly string _logDirectory;
    private readonly string _configPath;
    private readonly string _logPath;
    private Process? _ownedGuiProcess;
    private bool _ownsConnection;
    private bool _disposed;
    private DateTime? _startTime;
    private long _lastReceived;
    private long _lastSent;

    public bool IsConnected { get; private set; }
    public event Action<string>? StatusChanged;

    public OpenVpnService()
    {
        var baseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "OpenVPN");

        _configDirectory = Path.Combine(baseDirectory, "config");
        _logDirectory = Path.Combine(baseDirectory, "log");
        _configPath = Path.Combine(_configDirectory, $"{ProfileName}.ovpn");
        _logPath = Path.Combine(_logDirectory, $"{ProfileName}.log");
    }

    public async Task ConnectAsync(VpnServer server)
    {
        ArgumentNullException.ThrowIfNull(server);

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            EnsureOpenVpnGuiExists();

            if (IsConnected)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(server.OpenVPN_ConfigData_Base64))
            {
                throw new InvalidOperationException("선택한 서버에 OpenVPN 설정이 없습니다.");
            }

            PublishStatus($"OpenVPN 프로필 준비 중: {server.CountryLong}");

            Directory.CreateDirectory(_configDirectory);
            Directory.CreateDirectory(_logDirectory);

            // 기존 프로세스 정리 (로그 파일 잠금 해제 목적)
            CleanupExistingProcesses();

            if (File.Exists(_logPath))
            {
                try
                {
                    File.Delete(_logPath);
                }
                catch (IOException)
                {
                    // 로그 파일이 잠겨있어도 진행 (OpenVPN이 이어서 쓰게 됨)
                }
            }

            await File.WriteAllTextAsync(
                _configPath,
                BuildConfig(server.OpenVPN_ConfigData_Base64),
                Encoding.ASCII).ConfigureAwait(false);

            PublishStatus("OpenVPN GUI 시작 중...");
            EnsureGuiRunning();
            await Task.Delay(1000).ConfigureAwait(false);

            PublishStatus("기존 OpenVPN 연결 정리 중...");
            StartGui($"--command disconnect {ProfileName}");
            await Task.Delay(1000).ConfigureAwait(false);

            PublishStatus("OpenVPN 터널 연결 시도 중...");
            StartGui($"--silent_connection 1 --connect {ProfileName}");
            await WaitForLogAsync(ConnectTimeout, IsSuccessfulConnectLine, IsConnectionFailureLine).ConfigureAwait(false);

            IsConnected = true;
            _ownsConnection = true;
            _startTime = DateTime.Now;
            _lastReceived = 0;
            _lastSent = 0;
            PublishStatus("OpenVPN 터널 연결 완료");
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

            if (!File.Exists(GuiPath))
            {
                IsConnected = false;
                _ownsConnection = false;
                _startTime = null;
                return;
            }

            if (!_ownsConnection && !IsConnected)
            {
                return;
            }

            PublishStatus("OpenVPN 연결 해제 중...");
            StartGui($"--command disconnect {ProfileName}");

            try
            {
                await WaitForLogAsync(DisconnectTimeout, IsSuccessfulDisconnectLine, static _ => false).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
            }

            IsConnected = false;
            _ownsConnection = false;
            _startTime = null;
            PublishStatus("OpenVPN 연결 해제 완료");
        }
        finally
        {
            _gate.Release();
        }
    }

    public VpnStatistics GetStatistics()
    {
        // OpenVPN은 TAP 어댑터 또는 Wintun 어댑터를 사용함
        var stats = StatisticsHelper.GetInterfaceStatistics("TAP", _startTime, _lastReceived, _lastSent);
        if (stats.BytesReceived == 0)
        {
            stats = StatisticsHelper.GetInterfaceStatistics("Wintun", _startTime, _lastReceived, _lastSent);
        }
        
        _lastReceived = stats.BytesReceived;
        _lastSent = stats.BytesSent;

        return stats;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (_ownsConnection || IsConnected)
            {
                DisconnectAsync().GetAwaiter().GetResult();
            }
            ShutdownOwnedGuiProcess();
        }
        catch
        {
        }

        _gate.Dispose();
        _disposed = true;
    }

    private static bool IsSuccessfulConnectLine(string line) =>
        line.Contains("Initialization Sequence Completed", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("CONNECTED,SUCCESS", StringComparison.OrdinalIgnoreCase);

    private static bool IsSuccessfulDisconnectLine(string line) =>
        line.Contains("EXITING,SIGTERM", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("SIGTERM[hard", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("SIGTERM[soft", StringComparison.OrdinalIgnoreCase);

    private static bool IsConnectionFailureLine(string line) =>
        line.Contains("AUTH_FAILED", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("OPTIONS ERROR", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("process-push-msg-failed", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("Cannot open", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("Exiting due to fatal error", StringComparison.OrdinalIgnoreCase);

    private string BuildConfig(string base64Config)
    {
        var configText = Encoding.UTF8.GetString(Convert.FromBase64String(base64Config));
        var builder = new StringBuilder(configText.Trim());

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.AppendLine("data-ciphers DEFAULT:AES-128-CBC");
        builder.AppendLine("data-ciphers-fallback AES-128-CBC");
        builder.AppendLine("disable-dco");
        builder.AppendLine($"log \"{NormalizePath(_logPath)}\"");

        return builder.ToString();
    }

    private void EnsureOpenVpnGuiExists()
    {
        if (!File.Exists(GuiPath))
        {
            throw new FileNotFoundException(
                "OpenVPN 방식은 선택했지만 OpenVPN GUI를 찾지 못했습니다. OpenVPN을 설치하거나 Windows 기본 VPN 방식을 선택해 주세요.",
                GuiPath);
        }
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private void EnsureGuiRunning()
    {
        if (GetRunningGuiProcess() != null)
        {
            return;
        }

        _ownedGuiProcess = StartGui("--silent_connection 1");
    }

    private Process? StartGui(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = GuiPath,
            Arguments = arguments,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        return Process.Start(startInfo);
    }

    private async Task WaitForLogAsync(
        TimeSpan timeout,
        Func<string, bool> successPredicate,
        Func<string, bool> failurePredicate)
    {
        var startedAt = DateTime.UtcNow;
        var knownLength = 0;

        while (DateTime.UtcNow - startedAt < timeout)
        {
            if (File.Exists(_logPath))
            {
                var content = await ReadLogContentAsync().ConfigureAwait(false);
                if (content.Length != knownLength)
                {
                    knownLength = content.Length;
                    var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (failurePredicate(line))
                        {
                            PublishStatus(FormatLogLine(line));
                            throw new InvalidOperationException(line.Trim());
                        }

                        PublishStatus(FormatLogLine(line));

                        if (successPredicate(line))
                        {
                            return;
                        }
                    }
                }
            }

            await Task.Delay(500).ConfigureAwait(false);
        }

        throw new TimeoutException("OpenVPN 연결 상태를 확인하는 시간이 초과되었습니다.");
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void ShutdownOwnedGuiProcess()
    {
        if (_ownedGuiProcess == null)
        {
            return;
        }

        try
        {
            if (!_ownedGuiProcess.HasExited)
            {
                _ownedGuiProcess.Kill(entireProcessTree: false);
                _ownedGuiProcess.WaitForExit(3000);
            }
        }
        catch
        {
        }
        finally
        {
            _ownedGuiProcess.Dispose();
            _ownedGuiProcess = null;
        }
    }

    private static Process? GetRunningGuiProcess()
    {
        foreach (var process in Process.GetProcessesByName("openvpn-gui"))
        {
            try
            {
                if (string.Equals(process.MainModule?.FileName, GuiPath, StringComparison.OrdinalIgnoreCase))
                {
                    return process;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static void CleanupExistingProcesses()
    {
        foreach (var name in new[] { "openvpn", "openvpn-gui" })
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                try
                {
                    process.Kill(true);
                    process.WaitForExit(2000);
                }
                catch
                {
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
    }

    private void PublishStatus(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            StatusChanged?.Invoke(message.Trim());
        }
    }

    private static string FormatLogLine(string line)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        var firstSpace = trimmed.IndexOf(' ');
        if (firstSpace > 0 && trimmed.Length > firstSpace + 1)
        {
            var secondSpace = trimmed.IndexOf(' ', firstSpace + 1);
            if (secondSpace > 0 && trimmed.Length > secondSpace + 1)
            {
                return trimmed[(secondSpace + 1)..];
            }
        }

        return trimmed;
    }

    private async Task<string> ReadLogContentAsync()
    {
        using var stream = new FileStream(
            _logPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }
}
