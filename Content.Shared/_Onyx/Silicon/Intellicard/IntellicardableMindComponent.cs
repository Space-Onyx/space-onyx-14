// Content taken from Goob-Station (https://github.com/Goob-Station/Goob-Station/pull/6988), licensed under AGPL-3.0-or-later.

using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Silicon.Intellicard;

/// <summary>
/// Allows an entity's mind to be transferred to or from an intellicard.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IntellicardableMindComponent : Component
{
    [DataField, AutoNetworkedField]
    public float DownloadTimeFactor = 0.3f;

    [DataField, AutoNetworkedField]
    public float UploadTimeFactor = 0.8f;
}
