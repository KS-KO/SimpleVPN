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
#!/bin/bash
set -e

# sudo 사용 가능 여부 확인
SUDO_CMD=""""
if command -v sudo &> /dev/null; then
    SUDO_CMD=""sudo""
fi

# sing-box 설치를 위한 배포판 감지 및 패키지 매니저 실행
if ! command -v sing-box &> /dev/null; then
    if command -v apt-get &> /dev/null; then
        $SUDO_CMD apt-get update
        $SUDO_CMD apt-get install -y curl gpg
        $SUDO_CMD mkdir -p /etc/apt/keyrings
        curl -fsSL https://sing-box.app/gpg.key | $SUDO_CMD gpg --dearmor --yes -o /etc/apt/keyrings/sing-box.gpg
        $SUDO_CMD chmod a+r /etc/apt/keyrings/sing-box.gpg
        echo ""deb [signed-by=/etc/apt/keyrings/sing-box.gpg] https://deb.sagernet.org/ sing-box main"" | $SUDO_CMD tee /etc/apt/sources.list.d/sing-box.list
        $SUDO_CMD apt-get update
        $SUDO_CMD apt-get install -y sing-box
    elif command -v dnf &> /dev/null; then
        $SUDO_CMD dnf config-manager --add-repo https://yum.sagernet.org/sing-box.repo
        $SUDO_CMD dnf install -y sing-box
    elif command -v yum &> /dev/null; then
        $SUDO_CMD yum-config-manager --add-repo https://yum.sagernet.org/sing-box.repo
        $SUDO_CMD yum install -y sing-box
    else
        echo ""지원되지 않는 패키지 매니저입니다. 직접 sing-box를 설치해 주세요.""
        exit 1
    fi
fi

# 설정값 생성
UUID=$(sing-box generate uuid)
KEYPAIR=$(sing-box generate reality-keypair)
PRIVATE_KEY=$(echo ""$KEYPAIR"" | grep ""Private key:"" | awk ""{print \$3}"")
PUBLIC_KEY=$(echo ""$KEYPAIR"" | grep ""Public key:"" | awk ""{print \$3}"")
SHORT_ID=$(sing-box generate rand --hex 8)

# 설정 파일 작성
$SUDO_CMD mkdir -p /etc/sing-box
cat <<EOF | $SUDO_CMD tee /etc/sing-box/config.json
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

$SUDO_CMD systemctl restart sing-box || $SUDO_CMD sing-box run -c /etc/sing-box/config.json &
$SUDO_CMD systemctl enable sing-box &> /dev/null || true

# 클라이언트용 결과 출력
echo ""RESULT_START""
echo ""UUID: $UUID""
echo ""PUBLIC_KEY: $PUBLIC_KEY""
echo ""SHORT_ID: $SHORT_ID""
echo ""SNI: www.google.com""
echo ""PORT: 443""
echo ""RESULT_END""
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
