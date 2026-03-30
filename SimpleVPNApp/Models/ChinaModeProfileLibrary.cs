using System.Collections.Generic;

namespace SimpleVPNApp.Models;

/// <summary>
/// 여러 China Mode 저장 슬롯과 현재 선택 슬롯을 담습니다.
/// </summary>
public class ChinaModeProfileLibrary
{
    public string SelectedSavedProfileId { get; init; } = string.Empty;
    public List<ChinaModeSavedProfile> Profiles { get; init; } = new();
}
