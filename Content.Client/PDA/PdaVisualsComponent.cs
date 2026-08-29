using Robust.Shared.Utility; // <Onyx-PdaScreenVisuals>

namespace Content.Client.PDA;

/// <summary>
/// Used for visualizing PDA visuals.
/// </summary>
[RegisterComponent]
public sealed partial class PdaVisualsComponent : Component
{
    public string? BorderColor;

    public string? AccentHColor;

    public string? AccentVColor;

    public SpriteSpecifier? LastScreen; // <Onyx-PdaScreenVisuals>
}
