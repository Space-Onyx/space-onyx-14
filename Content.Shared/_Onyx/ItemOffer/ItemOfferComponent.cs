using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.ItemOffer;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedItemOfferSystem))]
public sealed partial class ItemOfferComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool IsInOfferMode;

    [DataField, AutoNetworkedField]
    public bool IsInReceiveMode;

    [DataField, AutoNetworkedField]
    public string? Hand;

    [DataField, AutoNetworkedField]
    public EntityUid? Item;

    [DataField, AutoNetworkedField]
    public EntityUid? Target;

    [DataField]
    public float MaxOfferDistance = 2f;
}
