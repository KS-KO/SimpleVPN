using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimpleVPNApp.Models;

namespace SimpleVPNApp.Services;

/// <summary>
/// Windows 기본 VPN 클라이언트(L2TP/IPsec)를 사용해 연결을 관리합니다.
/// 별도 OpenVPN 설치 없이 Windows 내장 기능만 사용합니다.
/// </summary>
public sealed class WindowsBuiltInVpnService : IVpnService
{
    private const string ConnectionName = "SimpleVPN";
    private const string SharedSecret = "vpn";
    private const string UserName = "vpn";
    private const string Password = "vpn";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;
    private DateTime? _startTime;
    private long _lastReceived;
    private long _lastSent;
    private long _currentDownloadSpeed;
    private long _currentUploadSpeed;

    public bool IsConnected { get; private set; }
    public event Action<string>? StatusChanged;

    public async Task ConnectAsync(VpnServer server)
    {
        ArgumentNullException.ThrowIfNull(server);

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(server.IP))
            {
                throw new InvalidOperationException("선택한 서버의 IP 정보가 없습니다.");
            }

            PublishStatus($"Windows VPN 프로필 구성 중: {server.CountryLong}");
            await EnsureConnectionProfileAsync(server.IP).ConfigureAwait(false);

            PublishStatus("Windows 기본 VPN 연결 시도 중...");
            var connectResult = await RunProcessAsync(
                "rasdial.exe",
                $"\"{ConnectionName}\" {UserName} {Password}").ConfigureAwait(false);

            if (connectResult.ExitCode != 0)
            {
                throw new InvalidOperationException(GetUsefulMessage(connectResult.Output, "Windows VPN 연결에 실패했습니다."));
            }

            IsConnected = true;
            _startTime = DateTime.Now;
            _lastReceived = 0;
            _lastSent = 0;
            PublishStatus("Windows 기본 VPN 연결 완료");
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

            PublishStatus("Windows 기본 VPN 연결 해제 중...");
            var disconnectResult = await RunProcessAsync(
                "rasdial.exe",
                $"\"{ConnectionName}\" /disconnect").ConfigureAwait(false);

            if (disconnectResult.ExitCode == 0 ||
                disconnectResult.Output.Contains("No connections", StringComparison.OrdinalIgnoreCase) ||
                disconnectResult.Output.Contains("명령을 찾을 수 없습니다", StringComparison.OrdinalIgnoreCase))
            {
                IsConnected = false;
                _startTime = null;
                PublishStatus("Windows 기본 VPN 연결 해제 완료");
                return;
            }

            throw new InvalidOperationException(GetUsefulMessage(disconnectResult.Output, "Windows VPN 연결 해제에 실패했습니다."));
        }
        finally
        {
            _gate.Release();
        }
    }

    public VpnStatistics GetStatistics()
    {
        if (!IsConnected || _startTime == null)
        {
            return new VpnStatistics { Duration = TimeSpan.Zero };
        }

        try
        {
            var interfaceStats = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(i => i.Name.Contains(ConnectionName, StringComparison.OrdinalIgnoreCase))
                ?.GetIPStatistics();

            if (interfaceStats != null)
            {
                var currentReceived = interfaceStats.BytesReceived;
                var currentSent = interfaceStats.BytesSent;

                if (_lastReceived > 0)
                {
                    _currentDownloadSpeed = currentReceived - _lastReceived;
                    _currentUploadSpeed = currentSent - _lastSent;
                }

                _lastReceived = currentReceived;
                _lastSent = currentSent;

                return new VpnStatistics
                {
                    BytesReceived = currentReceived,
                    BytesSent = currentSent,
                    DownloadSpeed = _currentDownloadSpeed,
                    UploadSpeed = _currentUploadSpeed,
                    Duration = DateTime.Now - _startTime.Value
                };
            }
        }
        catch
        {
            // 통계 조회 실패 시 기본값 반환
        }

        return new VpnStatistics { Duration = DateTime.Now - _startTime.Value };
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

    private async Task EnsureConnectionProfileAsync(string serverIp)
    {
        var escapedName = EscapeSingleQuotedPowerShellString(ConnectionName);
        var escapedServerIp = EscapeSingleQuotedPowerShellString(serverIp);
        var escapedPsk = EscapeSingleQuotedPowerShellString(SharedSecret);

        var script = $@"
$ErrorActionPreference = 'Stop'
$name = '{escapedName}'
$server = '{escapedServerIp}'
$psk = '{escapedPsk}'

try {{
    $existing = Get-VpnConnection -Name $name -ErrorAction SilentlyContinue
    if ($existing) {{
        rasdial.exe ""$name"" /disconnect | Out-Null
        Remove-VpnConnection -Name $name -Force -ErrorAction SilentlyContinue | Out-Null
    }}
}}
catch {{
}}

Add-VpnConnection -Name $name -ServerAddress $server -TunnelType L2tp -L2tpPsk $psk -EncryptionLevel Optional -AuthenticationMethod Pap,MSChapv2 -RememberCredential -Force | Out-Null
";

        var result = await RunProcessAsync(
            "powershell.exe",
            $"-NoProfile -EncodedCommand {EncodePowerShellScript(script)}").ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(GetUsefulMessage(result.Output, "Windows VPN 프로필 생성에 실패했습니다."));
        }
    }

    private static async Task<(int ExitCode, string Output)> RunProcessAsync(string fileName, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdOut = process.StandardOutput.ReadToEndAsync();
        var stdErr = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync().ConfigureAwait(false);

        var output = new StringBuilder();
        output.AppendLine(await stdOut.ConfigureAwait(false));
        output.AppendLine(await stdErr.ConfigureAwait(false));

        return (process.ExitCode, output.ToString());
    }

    private static string EscapeSingleQuotedPowerShellString(string value) => value.Replace("'", "''");

    private static string EncodePowerShellScript(string script) =>
        Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

    private static string GetUsefulMessage(string output, string fallback)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return fallback;
        }

        var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        for (var index = lines.Length - 1; index >= 0; index--)
        {
            var line = lines[index].Trim();
            if (!string.IsNullOrWhiteSpace(line))
            {
                return line;
            }
        }

        return fallback;
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
}
