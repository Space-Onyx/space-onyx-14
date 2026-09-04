// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Shared.Genetics.Systems;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Genetics;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedDnaModifierSystem))]
public sealed partial class DnaModifierInjectorComponent : Component
{

    [ViewVariables(VVAccess.ReadOnly), DataField("uniqueIdentifiers")]
    public UniqueIdentifiersData? UniqueIdentifiers { get; set; } = default!;

    [ViewVariables(VVAccess.ReadOnly)]
    public List<EnzymesPrototypeInfo>? EnzymesPrototypes { get; set; } = default!;

    [DataField("injectSound")]
    public SoundSpecifier InjectSound = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");
}

[RegisterComponent, Access(typeof(SharedDnaModifierSystem))]
public sealed partial class DnaModifierCleanRandomizeComponent : Component;
