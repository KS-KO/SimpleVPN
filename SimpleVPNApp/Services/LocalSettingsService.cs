using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SimpleVPNApp.Models;

namespace SimpleVPNApp.Services;

/// <summary>
/// 앱 로컬 설정 파일을 읽고 저장합니다.
/// </summary>
public sealed class LocalSettingsService
{
    private readonly string _settingsPath;
    private readonly CredentialService _credentialService = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public LocalSettingsService()
    {
        var settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SimpleVPN");
        _settingsPath = Path.Combine(settingsDirectory, "settings.json");
    }

    public async Task<ChinaModeSettings> LoadChinaModeSettingsAsync()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new ChinaModeSettings();
            }

            var json = await File.ReadAllTextAsync(_settingsPath).ConfigureAwait(false);
            var envelope = JsonSerializer.Deserialize<AppSettingsEnvelope>(json);
            var settings = envelope?.ChinaMode ?? new ChinaModeSettings();
            UnprotectSettings("main", settings);
            return settings;
        }
        catch
        {
            return new ChinaModeSettings();
        }
    }

    public async Task<ChinaModeProfileLibrary> LoadChinaModeProfileLibraryAsync()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return CreateDefaultLibrary();
            }

            var json = await File.ReadAllTextAsync(_settingsPath).ConfigureAwait(false);
            var settings = JsonSerializer.Deserialize<AppSettingsEnvelope>(json);

            if (settings?.ChinaModeLibrary?.Profiles.Count > 0)
            {
                foreach (var profile in settings.ChinaModeLibrary.Profiles)
                {
                    UnprotectSettings(profile.Id, profile.Settings);
                }
                return settings.ChinaModeLibrary;
            }

            if (settings?.ChinaMode != null)
            {
                UnprotectSettings("main", settings.ChinaMode);
                return new ChinaModeProfileLibrary
                {
                    SelectedSavedProfileId = "default",
                    Profiles =
                    [
                        new ChinaModeSavedProfile
                        {
                            Id = "default",
                            Name = "기본 프로필",
                            Settings = settings.ChinaMode
                        }
                    ]
                };
            }

            return CreateDefaultLibrary();
        }
        catch
        {
            return CreateDefaultLibrary();
        }
    }

    public async Task<bool> LoadKillSwitchStatusAsync()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return false;
            var json = await File.ReadAllTextAsync(_settingsPath).ConfigureAwait(false);
            var envelope = JsonSerializer.Deserialize<AppSettingsEnvelope>(json);
            return envelope?.IsKillSwitchEnabled ?? false;
        }
        catch { return false; }
    }

    public async Task<System.Collections.Generic.List<VpnServer>> LoadCustomServersAsync()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return new();
            var json = await File.ReadAllTextAsync(_settingsPath).ConfigureAwait(false);
            var envelope = JsonSerializer.Deserialize<AppSettingsEnvelope>(json);
            return envelope?.CustomServers ?? new();
        }
        catch { return new(); }
    }

    public async Task SaveAppSettingsAsync(ChinaModeSettings chinaSettings, ChinaModeProfileLibrary library, bool isKillSwitchEnabled, System.Collections.Generic.List<VpnServer> customServers)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        // 원본 객체 손상 방지를 위해 클론 후 보호된 상태로 직렬화
        var protectedChinaMode = CloneAndProtect("main", chinaSettings);
        var protectedLibrary = new ChinaModeProfileLibrary
        {
            SelectedSavedProfileId = library.SelectedSavedProfileId,
            Profiles = library.Profiles.Select(p => new ChinaModeSavedProfile
            {
                Id = p.Id,
                Name = p.Name,
                Settings = CloneAndProtect(p.Id, p.Settings)
            }).ToList()
        };

        var payload = new AppSettingsEnvelope
        {
            IsKillSwitchEnabled = isKillSwitchEnabled,
            ChinaMode = protectedChinaMode,
            ChinaModeLibrary = protectedLibrary,
            CustomServers = customServers
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await File.WriteAllTextAsync(_settingsPath, json).ConfigureAwait(false);
    }

    private ChinaModeSettings CloneAndProtect(string profileId, ChinaModeSettings original)
    {
        var cloned = new ChinaModeSettings
        {
            SelectedProfileKey = original.SelectedProfileKey,
            OutlineAccessKey = original.OutlineAccessKey,
            OutlineApiUrl = original.OutlineApiUrl,
            OutlineCertSha256 = original.OutlineCertSha256,
            OutlineSshHost = original.OutlineSshHost,
            OutlineSshUser = original.OutlineSshUser,
            OutlineSshKeyPath = original.OutlineSshKeyPath,
            OutlineProvisionHostname = original.OutlineProvisionHostname,
            OutlineProvisionPort = original.OutlineProvisionPort,
            VlessRealityServer = original.VlessRealityServer,
            VlessRealityPort = original.VlessRealityPort,
            VlessRealityUuid = original.VlessRealityUuid,
            VlessRealityPublicKey = original.VlessRealityPublicKey,
            VlessRealityShortId = original.VlessRealityShortId,
            VlessRealityServerName = original.VlessRealityServerName,
            VlessRealityFingerprint = original.VlessRealityFingerprint,
            TrojanServer = original.TrojanServer,
            TrojanPort = original.TrojanPort,
            TrojanPassword = original.TrojanPassword,
            TrojanServerName = original.TrojanServerName,
            TrojanFingerprint = original.TrojanFingerprint
        };

        if (!string.IsNullOrEmpty(cloned.OutlineAccessKey) && cloned.OutlineAccessKey != "[PROTECTED]")
        {
            _credentialService.SaveCredential($"{profileId}:OutlineAccessKey", cloned.OutlineAccessKey);
            cloned.OutlineAccessKey = "[PROTECTED]";
        }

        if (!string.IsNullOrEmpty(cloned.VlessRealityUuid) && cloned.VlessRealityUuid != "[PROTECTED]")
        {
            _credentialService.SaveCredential($"{profileId}:VlessRealityUuid", cloned.VlessRealityUuid);
            cloned.VlessRealityUuid = "[PROTECTED]";
        }

        if (!string.IsNullOrEmpty(cloned.TrojanPassword) && cloned.TrojanPassword != "[PROTECTED]")
        {
            _credentialService.SaveCredential($"{profileId}:TrojanPassword", cloned.TrojanPassword);
            cloned.TrojanPassword = "[PROTECTED]";
        }

        return cloned;
    }

    private void UnprotectSettings(string profileId, ChinaModeSettings settings)
    {
        if (settings.OutlineAccessKey == "[PROTECTED]")
        {
            var outlineKey = _credentialService.ReadCredential($"{profileId}:OutlineAccessKey");
            if (string.IsNullOrEmpty(outlineKey) && profileId != "main")
            {
                outlineKey = _credentialService.ReadCredential("main:OutlineAccessKey");
            }
            if (!string.IsNullOrEmpty(outlineKey)) settings.OutlineAccessKey = outlineKey;
        }

        if (settings.VlessRealityUuid == "[PROTECTED]")
        {
            var vlessUuid = _credentialService.ReadCredential($"{profileId}:VlessRealityUuid");
            if (string.IsNullOrEmpty(vlessUuid) && profileId != "main")
            {
                vlessUuid = _credentialService.ReadCredential("main:VlessRealityUuid");
            }
            if (!string.IsNullOrEmpty(vlessUuid)) settings.VlessRealityUuid = vlessUuid;
        }

        if (settings.TrojanPassword == "[PROTECTED]")
        {
            var trojanPassword = _credentialService.ReadCredential($"{profileId}:TrojanPassword");
            if (string.IsNullOrEmpty(trojanPassword) && profileId != "main")
            {
                trojanPassword = _credentialService.ReadCredential("main:TrojanPassword");
            }
            if (!string.IsNullOrEmpty(trojanPassword)) settings.TrojanPassword = trojanPassword;
        }
    }

    public async Task ExportChinaModeSettingsAsync(string path, ChinaModeSettings settings)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
    }

    public async Task<ChinaModeSettings> ImportChinaModeSettingsAsync(string path)
    {
        var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        return JsonSerializer.Deserialize<ChinaModeSettings>(json) ?? new ChinaModeSettings();
    }

    private sealed class AppSettingsEnvelope
    {
        public bool IsKillSwitchEnabled { get; init; }
        public ChinaModeSettings ChinaMode { get; init; } = new();
        public ChinaModeProfileLibrary ChinaModeLibrary { get; init; } = new();
        public System.Collections.Generic.List<VpnServer> CustomServers { get; init; } = new();
    }

    private static ChinaModeProfileLibrary CreateDefaultLibrary() =>
        new()
        {
            SelectedSavedProfileId = "default",
            Profiles =
            [
                new ChinaModeSavedProfile
                {
                    Id = "default",
                    Name = "기본 프로필",
                    Settings = new ChinaModeSettings()
                }
            ]
        };
}
