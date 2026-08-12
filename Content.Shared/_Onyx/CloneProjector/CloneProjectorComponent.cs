using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.CloneProjector;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CloneProjectorComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid? CloneUid;

    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid? CurrentHost;

    [DataField]
    public TimeSpan DestroyedCooldown = TimeSpan.FromSeconds(90);

    [DataField]
    public TimeSpan StunDuration = TimeSpan.FromSeconds(8);

    [DataField]
    public bool DoStun = true;

    [DataField]
    public DamageSpecifier DamageOnDestroyed = new();

    [DataField]
    public bool RestrictRangedWeapons = true;

    [DataField]
    public ComponentRegistry? AddedComponents;

    [DataField]
    public ComponentRegistry? RemovedComponents;

    [DataField]
    public EntityWhitelist? ClonedItemBlacklist;

    [DataField]
    public EntityWhitelist? ClonedItemWhitelist;

    [DataField]
    public EntityWhitelist? UserBlacklist;

    [DataField]
    public ProtoId<DamageModifierSetPrototype> CloneDamageModifierSet = "LivingLight";

    [DataField]
    public LocId NameSuffix = "gemini-projector-clone-name-suffix";

    [DataField]
    public LocId FlavorText = "gemini-projector-clone-flavor-text";

    [DataField]
    public LocId CloneGeneratedMessage = "gemini-projector-clone-created";

    [DataField]
    public LocId CloneRetrievedMessage = "gemini-projector-clone-retrieved";

    [DataField]
    public LocId EquippedMessage = "gemini-projector-installed";

    [DataField]
    public LocId UnequippedMessage = "gemini-projector-removed";

    [DataField]
    public LocId GhostRoleName = "ghost-role-information-gemini-clone-name";

    [DataField]
    public LocId GhostRoleDescription = "ghost-role-information-gemini-clone-description";

    [DataField]
    public LocId GhostRoleRules = "ghost-role-information-familiar-rules";

    [DataField]
    public ProtoId<PlayTimeTrackerPrototype>? RequiredRole = "JobScientist";

    [DataField]
    public TimeSpan TimeNeeded = TimeSpan.FromSeconds(18000);

    [ViewVariables(VVAccess.ReadOnly)]
    public Container CloneContainer = new();

    [DataField]
    public EntProtoId Action = "ActionActivateProjector";

    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid? ActionEntity;
}
