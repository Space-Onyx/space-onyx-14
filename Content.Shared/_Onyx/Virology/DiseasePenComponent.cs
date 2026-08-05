using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Virology;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DiseasePenComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public int? Genotype;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? DiseaseUid;

    [ViewVariables, AutoNetworkedField]
    public bool Used = false;

    [DataField, ViewVariables]
    public SoundSpecifier InjectSound = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");

    [DataField, AutoNetworkedField]
    public bool Vaccine = true;

    [DataField]
    public TimeSpan InjectTime = TimeSpan.FromSeconds(8);
};
