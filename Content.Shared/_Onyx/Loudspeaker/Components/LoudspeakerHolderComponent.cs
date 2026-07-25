namespace Content.Shared._Onyx.Loudspeaker.Components;

[RegisterComponent]
public sealed partial class LoudspeakerHolderComponent : Component
{
    [DataField]
    public List<EntityUid> Loudspeakers = new();
}
