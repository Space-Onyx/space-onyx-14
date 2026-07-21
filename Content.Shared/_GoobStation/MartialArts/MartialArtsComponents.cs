using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.MartialArts;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class MartialArtsKnowledgeComponent : Component
{
    [DataField, AutoNetworkedField]
    public MartialArtsForms MartialArtsForm;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CanPerformComboComponent : Component
{
    [AutoNetworkedField] public EntityUid? CurrentTarget;
    [AutoNetworkedField] public List<ComboAttackType> LastAttacks = new();
    [DataField] public TimeSpan ResetAfter = TimeSpan.FromSeconds(5);
    public TimeSpan ResetAt;
}

[RegisterComponent]
public sealed partial class GrantMartialArtKnowledgeComponent : Component
{
    [DataField(required: true)] public MartialArtsForms MartialArtsForm;
    [DataField] public bool MultiUse;
}

[RegisterComponent]
public sealed partial class KravMagaSilencedComponent : Component
{
    public TimeSpan Until;
}

[RegisterComponent]
public sealed partial class KravMagaBlockedBreathingComponent : Component
{
    public TimeSpan Until;
}
