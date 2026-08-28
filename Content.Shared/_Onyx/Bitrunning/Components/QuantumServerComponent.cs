using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Generic;
using Content.Shared._Onyx.Bitrunning.Prototypes;
using Robust.Shared.Audio;

namespace Content.Shared._Onyx.Bitrunning.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class QuantumServerComponent : Component
{
    // This is intentionally unbounded so avatars and linked pods always remain viewable on camera networks.
    private const int UnboundedBroadcastRange = int.MaxValue;

    [DataField, AutoNetworkedField]
    public BitrunningServerState State = BitrunningServerState.Ready;

    [DataField, AutoNetworkedField]
    public int Points;

    [DataField, AutoNetworkedField]
    public int ScannerTier = 1;

    [DataField, AutoNetworkedField]
    public float CooldownMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public float QualityBonus;

    [DataField]
    public float BaseExitDamageScale = 0.20f;

    [DataField, AutoNetworkedField]
    public float FinalExitDamageMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public EntProtoId AvatarPrototype = "MobHuman";

    /// <summary>
    /// Encrypted cache that spawns in the domain when players reach the goal.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId CompletionRewardCachePrototype = "BitrunningObjectiveCacheStructure";

    /// <summary>
    /// Crate that spawns in byteforge delivery.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId RewardCachePrototype = "CrateBitrunSecureReward";

    [DataField, AutoNetworkedField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(2);

    [DataField, AutoNetworkedField]
    public TimeSpan CooldownEndTime;

    [DataField, AutoNetworkedField]
    public bool BroadcastEnabled;

    [DataField]
    public int BroadcastWirelessRange = UnboundedBroadcastRange;

    [DataField]
    public TimeSpan ExitParalyzeTime = TimeSpan.FromSeconds(3.5);

    [DataField]
    public TimeSpan ExitBlindnessTime = TimeSpan.FromSeconds(3.5);

    [DataField(customTypeSerializer: typeof(ProtoIdSerializer<BitrunningVirtualDomainPrototype>)), AutoNetworkedField]
    public ProtoId<BitrunningVirtualDomainPrototype>? CurrentDomain;

    // Server-only runtime state. These fields are not synchronized to clients.
    public EntityUid? DomainMapUid;

    public EntityUid? DomainGridUid;

    public readonly HashSet<EntityUid> ActiveConnections = new();

    public EntityCoordinates? ExitCoordinates;

    public EntityCoordinates? CacheCoordinates;

    public bool HasExplicitCacheMarker;

    public EntityCoordinates? GoalCoordinates;

    public EntityCoordinates? SpawnCoordinates;

    public EntityUid? LinkedByteforge;

    public TimeSpan DomainStartTime;

    public int ObjectivePoints;

    public TimeSpan NextSatiationProgressTime;

    public int ObjectiveGoal;

    public bool ObjectiveCompleted;

    public BitrunningObjectiveType ObjectiveType = BitrunningObjectiveType.None;

    public int ThreatsDestroyed;

    public bool AllowDiskModifications = true;

    public bool AllowProfileLoad = true;

    public bool WasRandomizedRun;

    public readonly HashSet<EntityUid> GrantedItemDisks = new();

    public bool TechnologyDiskRewardSpawned;

    [DataField]
    public SoundSpecifier DomainStartSound = new SoundPathSpecifier("/Audio/Machines/terminal_insert_disc.ogg");

    [DataField]
    public SoundSpecifier DomainLoadedSound = new SoundPathSpecifier("/Audio/Machines/terminal_insert_disc.ogg");

    [DataField]
    public SoundSpecifier DomainStopSound = new SoundPathSpecifier("/Audio/_Onyx/Machines/terminal_off.ogg");

    [DataField]
    public SoundSpecifier DomainAlertSound = new SoundPathSpecifier("/Audio/Machines/terminal_insert_disc.ogg");

    [DataField]
    public SoundSpecifier ObjectiveRewardSound = new SoundPathSpecifier("/Audio/_Onyx/Machines/win.ogg");
}
