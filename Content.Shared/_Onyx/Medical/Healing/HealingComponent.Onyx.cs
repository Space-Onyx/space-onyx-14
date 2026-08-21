using Content.Shared._Onyx.Wounds;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Healing;

public sealed partial class HealingComponent
{
    /// <summary>Whether this item repairs damage stored on the selected body part.</summary>
    [DataField, AutoNetworkedField]
    public bool HealDamage = true;

    /// <summary>Whether this item also reduces severity of wounds mapped to the healed damage types.</summary>
    [DataField, AutoNetworkedField]
    public bool HealWounds;

    [DataField, AutoNetworkedField]
    public HashSet<TreatmentCapability> TreatmentCapabilities = [TreatmentCapability.Biological];

    [DataField, AutoNetworkedField]
    public HashSet<string>? AllowedWoundStages;
}
