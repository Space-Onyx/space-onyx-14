// Content taken from Goob-Station (https://github.com/Goob-Station/Goob-Station), licensed under AGPL-3.0-or-later.

using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.SpeakFontOverride;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class SpeakFontOverrideComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled;

    [DataField]
    public string? FontId;

    [DataField]
    public int? FontSize;

    [DataField]
    public Color? Color;
}
