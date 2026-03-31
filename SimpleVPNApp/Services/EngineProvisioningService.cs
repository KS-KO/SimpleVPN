using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace SimpleVPNApp.Services;

/// <summary>
/// VPN 엔진(sing-box)의 초기 실행 환경을 구축하고 자동 업데이트를 담당합니다.
/// </summary>
public class EngineProvisioningService
{
    private const string EngineVersion = "1.9.3";
    private const string DownloadUrl = $"https://github.com/SagerNet/sing-box/releases/download/v{EngineVersion}/sing-box-{EngineVersion}-windows-amd64.zip";
    
    private readonly string _runtimeDirectory;
    private readonly string _enginePath;

    public EngineProvisioningService()
    {
        _runtimeDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SimpleVPN",
            "sing-box");
        _enginePath = Path.Combine(_runtimeDirectory, "sing-box.exe");
    }

    public string GetEnginePath() => _enginePath;

    public async Task<bool> EnsureEngineReadyAsync(Action<string>? progressCallback = null)
    {
        if (File.Exists(_enginePath))
        {
            return true;
        }

        progressCallback?.Invoke("엔진이 없습니다. 자동 다운로드를 시작합니다...");
        
        try
        {
            Directory.CreateDirectory(_runtimeDirectory);
            var zipPath = Path.Combine(_runtimeDirectory, "sing-box.zip");

            using (var client = new HttpClient())
            {
                progressCallback?.Invoke($"sing-box v{EngineVersion} 다운로드 중...");
                var bytes = await client.GetByteArrayAsync(DownloadUrl).ConfigureAwait(false);
                await File.WriteAllBytesAsync(zipPath, bytes).ConfigureAwait(false);
            }

            progressCallback?.Invoke("압축 해제 및 설치 중...");
            
            // ZIP 파일 내의 구조: sing-box-1.9.3-windows-amd64/sing-box.exe
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith("sing-box.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        entry.ExtractToFile(_enginePath, overwrite: true);
                        break;
                    }
                }
            }

            File.Delete(zipPath);
            progressCallback?.Invoke("엔진 설치 완료");
            return true;
        }
        catch (Exception ex)
        {
            progressCallback?.Invoke($"엔진 설치 실패: {ex.Message}");
            return false;
        }
    }
}
