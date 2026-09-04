using Content.Shared._Onyx.Chat;

namespace Content.Client._Onyx.Chat;

public sealed class CustomEmoteEntry
{
    public string Name { get; set; } = string.Empty;
    public bool Custom { get; set; }
    public string? EmoteId { get; set; }
    public string? Text { get; set; }
    public EmotePerspective Perspective { get; set; } = EmotePerspective.FirstPerson;
    public EmoteVisibilityRange Range { get; set; } = EmoteVisibilityRange.Surrounding;
    public int Radius { get; set; } = EmoteVisibilityOptions.MinRadius;
    public bool ShowToGhosts { get; set; } = true;
    public string? SoundId { get; set; }
    public List<int> BindKeys { get; set; } = new();
}
