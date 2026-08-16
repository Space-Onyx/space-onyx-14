using Content.Shared.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Speech;

[RegisterComponent, NetworkedComponent]
[Access(typeof(RaspyAccentSystem))]
public sealed partial class RaspyAccentComponent : BaseAccentComponent
{
    [DataField]
    public List<LocId> Noises = new()
    {
        "raspy-accent-1",
        "raspy-accent-2",
        "raspy-accent-3",
        "raspy-accent-4",
        "raspy-accent-5",
        "raspy-accent-6",
        "raspy-accent-7",
    };
}
