using Content.Shared.Silicons.Laws;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.CustomLawboard;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class CustomLawboardComponent : Component
{
    public const int MaxLaws = 15;
    public const int MaxLawLength = 512;

    [DataField, AutoNetworkedField]
    public List<SiliconLaw> Laws = new();
}

[Serializable, NetSerializable]
public enum CustomLawboardUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class CustomLawboardChangeLawsMessage(List<string> laws) : BoundUserInterfaceMessage
{
    public readonly List<string> Laws = laws;
}
