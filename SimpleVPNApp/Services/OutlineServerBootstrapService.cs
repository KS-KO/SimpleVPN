using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SimpleVPN.Core.Models;

namespace SimpleVPNApp.Services;

public sealed class OutlineServerBootstrapService
{
    private const string InstallScript = "if command -v wget &> /dev/null; then sudo bash -c \"$(wget -qO- https://raw.githubusercontent.com/OutlineFoundation/outline-apps/master/server_manager/install_scripts/install_server.sh)\"; elif command -v curl &> /dev/null; then sudo bash -c \"$(curl -fsSL https://raw.githubusercontent.com/OutlineFoundation/outline-apps/master/server_manager/install_scripts/install_server.sh)\"; else echo 'wget or curl is required'; exit 1; fi";

    public async Task<OutlineServerBootstrapResult> BootstrapAsync(
        string host,
        string user,
        string sshKeyPath,
        string hostname,
        string keysPort)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException("새 서버 생성을 위해 서버 주소를 입력해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(user))
        {
            throw new InvalidOperationException("SSH 사용자 이름을 입력해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(sshKeyPath))
        {
            throw new InvalidOperationException("SSH 키 파일 경로를 입력해 주세요.");
        }

        var remote = $"{user.Trim()}@{host.Trim()}";
        var options = BuildInstallOptions(hostname, keysPort);
        var installCommand = string.IsNullOrWhiteSpace(options) ? InstallScript : $"{InstallScript} install_server.sh {options}";

        await RunSshAsync(sshKeyPath, remote, installCommand).ConfigureAwait(false);
        var accessText = await RunSshAsync(sshKeyPath, remote, "cat /opt/outline/access.txt").ConfigureAwait(false);

        return new OutlineServerBootstrapResult
        {
            Host = host.Trim(),
            ManagementApiUrl = ExtractJsonValue(accessText, "apiUrl"),
            CertificateSha256 = ExtractJsonValue(accessText, "certSha256")
        };
    }

    public async Task<OutlineServerBootstrapResult> ReadExistingServerAccessAsync(
        string host,
        string user,
        string sshKeyPath)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException("기존 서버 조회를 위해 서버 주소를 입력해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(user))
        {
            throw new InvalidOperationException("SSH 사용자 이름을 입력해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(sshKeyPath))
        {
            throw new InvalidOperationException("SSH 키 파일 경로를 입력해 주세요.");
        }

        var remote = $"{user.Trim()}@{host.Trim()}";
        var accessText = await RunSshAsync(sshKeyPath, remote, "cat /opt/outline/access.txt").ConfigureAwait(false);

        return new OutlineServerBootstrapResult
        {
            Host = host.Trim(),
            ManagementApiUrl = ExtractJsonValue(accessText, "apiUrl"),
            CertificateSha256 = ExtractJsonValue(accessText, "certSha256")
        };
    }

    private static string BuildInstallOptions(string hostname, string keysPort)
    {
        var options = string.Empty;

        if (!string.IsNullOrWhiteSpace(hostname))
        {
            options += $" --hostname={hostname.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(keysPort))
        {
            options += $" --keys-port={keysPort.Trim()}";
        }

        return options.Trim();
    }

    private static async Task<string> RunSshAsync(string sshKeyPath, string remote, string remoteCommand)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ssh",
            Arguments = $"-i \"{sshKeyPath}\" -o StrictHostKeyChecking=accept-new {remote} \"{remoteCommand}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
        }

        return stdout;
    }

    private static string ExtractJsonValue(string content, string key)
    {
        var match = Regex.Match(content, $"\"{Regex.Escape(key)}\"\\s*:\\s*\"(?<value>[^\"]+)\"");
        if (!match.Success)
        {
            throw new InvalidOperationException($"{key} 값을 설치 결과에서 찾지 못했습니다.");
        }

        return match.Groups["value"].Value;
    }
}
