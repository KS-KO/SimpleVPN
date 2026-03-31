using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace SimpleVPNApp.Services;

/// <summary>
/// 로컬 SSH 설정(~/.ssh/config)을 분석하여 알려진 서버 정보를 추출합니다.
/// </summary>
public static class SshConfigParserService
{
    public static IEnumerable<SshHostInfo> ParseKnownHosts()
    {
        var results = new List<SshHostInfo>();
        var sshConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ssh", "config");

        if (!File.Exists(sshConfigPath))
        {
            return results;
        }

        try
        {
            var lines = File.ReadAllLines(sshConfigPath);
            SshHostInfo? current = null;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

                var matchHost = Regex.Match(trimmed, @"^Host\s+(?<name>.+)$", RegexOptions.IgnoreCase);
                if (matchHost.Success)
                {
                    if (current != null) results.Add(current);
                    current = new SshHostInfo { Name = matchHost.Groups["name"].Value };
                    continue;
                }

                if (current == null) continue;

                var matchHostName = Regex.Match(trimmed, @"^HostName\s+(?<val>.+)$", RegexOptions.IgnoreCase);
                if (matchHostName.Success) { current.HostName = matchHostName.Groups["val"].Value; continue; }

                var matchUser = Regex.Match(trimmed, @"^User\s+(?<val>.+)$", RegexOptions.IgnoreCase);
                if (matchUser.Success) { current.User = matchUser.Groups["val"].Value; continue; }

                var matchKey = Regex.Match(trimmed, @"^IdentityFile\s+(?<val>.+)$", RegexOptions.IgnoreCase);
                if (matchKey.Success) { current.IdentityFile = matchKey.Groups["val"].Value.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)); continue; }
            }

            if (current != null) results.Add(current);
        }
        catch { /* ignored */ }

        return results;
    }

    public class SshHostInfo
    {
        public string Name { get; set; } = string.Empty;
        public string HostName { get; set; } = string.Empty;
        public string User { get; set; } = "root";
        public string IdentityFile { get; set; } = string.Empty;

        public override string ToString() => Name == HostName ? Name : $"{Name} ({HostName})";
    }
}
