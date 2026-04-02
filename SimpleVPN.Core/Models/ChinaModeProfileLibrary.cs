using System.Collections.Generic;

namespace SimpleVPN.Core.Models;

public class ChinaModeProfileLibrary
{
    public string SelectedSavedProfileId { get; init; } = string.Empty;
    public List<ChinaModeSavedProfile> Profiles { get; init; } = new();
}
