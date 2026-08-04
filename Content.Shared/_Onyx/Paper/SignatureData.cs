using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Paper;

[DataDefinition, Serializable, NetSerializable]
public partial struct SignatureDisplayInfo
{
    [DataField]
    public string SignedName;

    [DataField]
    public string FontId;

    [DataField]
    public int FontSize;

    [DataField]
    public Color SignColor;

    [DataField]
    public string HandwritingId;
}

[RegisterComponent]
public sealed partial class SignToolComponent : Component
{
    [DataField]
    public string FontId = "Sign";

    [DataField]
    public int FontSize = 16;

    [DataField]
    public Color SignColor = Color.DarkSlateGray;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SignatureIdentityComponent : Component
{
    [DataField, AutoNetworkedField]
    public string HandwritingId = string.Empty;
}

[ByRefEvent]
public record struct SignAttemptEvent(EntityUid Paper, EntityUid Signer, bool Cancelled = false);

[ByRefEvent]
public record struct BeingSignedAttemptEvent(EntityUid Paper, EntityUid Signer, bool Cancelled = false);

[ByRefEvent]
public record struct SignSuccessfulEvent(EntityUid Paper, EntityUid Signer);
