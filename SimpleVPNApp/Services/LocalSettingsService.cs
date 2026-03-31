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
            return envelope?.ChinaMode ?? new ChinaModeSettings();
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
                return settings.ChinaModeLibrary;
            }

            if (settings?.ChinaMode != null)
            {
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

        var payload = new AppSettingsEnvelope
        {
            IsKillSwitchEnabled = isKillSwitchEnabled,
            ChinaMode = chinaSettings,
            ChinaModeLibrary = library,
            CustomServers = customServers
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await File.WriteAllTextAsync(_settingsPath, json).ConfigureAwait(false);
    }

    public async Task SaveChinaModeSettingsAsync(ChinaModeSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var payload = new AppSettingsEnvelope
        {
            ChinaMode = settings
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);

        await File.WriteAllTextAsync(_settingsPath, json).ConfigureAwait(false);
    }

    public async Task SaveChinaModeProfileLibraryAsync(ChinaModeProfileLibrary library)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var payload = new AppSettingsEnvelope
        {
            ChinaMode = library.Profiles.FirstOrDefault(profile => profile.Id == library.SelectedSavedProfileId)?.Settings ?? new ChinaModeSettings(),
            ChinaModeLibrary = library
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await File.WriteAllTextAsync(_settingsPath, json).ConfigureAwait(false);
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
