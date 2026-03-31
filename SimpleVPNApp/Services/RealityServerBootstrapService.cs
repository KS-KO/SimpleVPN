using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SimpleVPNApp.Models;

namespace SimpleVPNApp.Services;

/// <summary>
/// 원격 Linux 서버인 경우 SSH를 통해 sing-box(VLESS REALITY)를 자동 설치하고 설정합니다.
/// </summary>
public sealed class RealityServerBootstrapService
{
    // 정해진 포트(443)와 도메인(google.com)을 기본으로 하는 sing-box VLESS REALITY 설치 스크립트 예시
    private const string InstallScript = @"
sudo bash -c '
# Install sing-box
if ! command -v sing-box &> /dev/null; then
    curl -fsSL https://sing-box.app/gpg.key | sudo gpg --dearmor -o /etc/apt/keyrings/sing-box.gpg
    sudo chmod a+r /etc/apt/keyrings/sing-box.gpg
    echo ""deb [signed-by=/etc/apt/keyrings/sing-box.gpg] https://deb.sagernet.org/ sing-box main"" | sudo tee /etc/apt/sources.list.d/sing-box.list
    sudo apt-get update
    sudo apt-get install -y sing-box
fi

# Generate keys
UUID=$(sing-box generate uuid)
KEYPAIR=$(sing-box generate reality-keypair)
PRIVATE_KEY=$(echo ""$KEYPAIR"" | grep ""Private key:"" | awk ""{print \$3}"")
PUBLIC_KEY=$(echo ""$KEYPAIR"" | grep ""Public key:"" | awk ""{print \$3}"")
SHORT_ID=$(sing-box generate rand --hex 8)

# Create config
cat <<EOF | sudo tee /etc/sing-box/config.json
{
  ""inbounds"": [
    {
      ""type"": ""vless"",
      ""tag"": ""vless-in"",
      ""listen"": ""::"",
      ""listen_port"": 443,
      ""users"": [{ ""uuid"": ""$UUID"", ""flow"": ""xtls-rprx-vision"" }],
      ""tls"": {
        ""enabled"": true,
        ""server_name"": ""www.google.com"",
        ""reality"": {
          ""enabled"": true,
          ""handshake"": { ""server"": ""www.google.com"", ""server_port"": 443 },
          ""private_key"": ""$PRIVATE_KEY"",
          ""short_id"": [ ""$SHORT_ID"" ]
        }
      }
    }
  ],
  ""outbounds"": [{ ""type"": ""direct"", ""tag"": ""direct"" }]
}
EOF

sudo systemctl restart sing-box
sudo systemctl enable sing-box

# Output for client
echo ""RESULT_START""
echo ""UUID: $UUID""
echo ""PUBLIC_KEY: $PUBLIC_KEY""
echo ""SHORT_ID: $SHORT_ID""
echo ""SNI: www.google.com""
echo ""PORT: 443""
echo ""RESULT_END""
'
";

    public async Task<VlessRealityConfig> BootstrapAsync(string host, string user, string sshKeyPath)
    {
        var remote = $"{user.Trim()}@{host.Trim()}";
        var output = await RunSshAsync(sshKeyPath, remote, InstallScript).ConfigureAwait(false);

        return new VlessRealityConfig
        {
            Server = host.Trim(),
            Port = ExtractValue(output, "PORT"),
            Uuid = ExtractValue(output, "UUID"),
            PublicKey = ExtractValue(output, "PUBLIC_KEY"),
            ShortId = ExtractValue(output, "SHORT_ID"),
            ServerName = ExtractValue(output, "SNI")
        };
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

    private static string ExtractValue(string content, string key)
    {
        var match = Regex.Match(content, $"{key}:\\s*(?<value>\\S+)");
        return match.Success ? match.Groups["value"].Value : string.Empty;
    }

    public class VlessRealityConfig
    {
        public string Server { get; set; } = string.Empty;
        public string Port { get; set; } = string.Empty;
        public string Uuid { get; set; } = string.Empty;
        public string PublicKey { get; set; } = string.Empty;
        public string ShortId { get; set; } = string.Empty;
        public string ServerName { get; set; } = string.Empty;
    }
}
