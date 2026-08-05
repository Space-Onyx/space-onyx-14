using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Disease.Components;

/// <summary>
/// For entities that have the ability to naturally fight back diseases
/// If you want to make some sort of alternate immunity of your own, copypaste and adjust SharedDiseaseSystem.Immunity.cs
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImmunityComponent : Component
{
    /// <summary>
    /// How fast this organism increases immune progress on diseases, per second
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ImmunityGainRate = 0.00111111111111f; // 900 seconds to heal fully

    /// <summary>
    /// How fast this organism decreases infection progress at full immunity progress
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ImmunityStrength = 0.0088888888888f;

    /// <summary>
    /// Which disease types can this affect the immunity strength against and gain immunity to
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<DiseaseTypePrototype>> AffectedTypes = new();

    /// <summary>
    /// Genotypes we have gained immunity against from getting sick by them or having taken a vaccine for
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<int> ImmuneTo = new();

    /// <summary>
    /// Whether to still work while dead
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool InDead = false;
}
