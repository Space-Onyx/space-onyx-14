using Content.Shared._Onyx.Body;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;
using Content.Shared.Tag;
using Content.Shared.Tools.Systems;
using Content.Shared.UserInterface;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Medical.Surgery;

public abstract partial class SharedSurgerySystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PainSystem _pain = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private RotateToFaceSystem _rotateToFace = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedStackSystem _stacks = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedToolSystem _tools = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    private const string CavityContainer = "surgery_cavity";
    private static readonly EntProtoId SurgicallyMutedEffect = "StatusEffectSurgicallyMuted";
    protected readonly Dictionary<(EntityUid Body, EntityUid Part), ActiveSurgerySite> ActiveSurgerySites = new();
    private List<PendingSurgeryRepeat> _pendingSurgeryRepeats = new();
    private List<PendingSurgeryRepeat> _processingSurgeryRepeats = new();
    private uint _nextSurgeryToken;

    protected readonly record struct ActiveSurgerySite(uint Token, EntityUid User);
    private readonly record struct PendingSurgeryRepeat(
        EntityUid Body,
        EntityUid Part,
        EntityUid User,
        EntProtoId Surgery,
        EntProtoId Step,
        uint Token);
}
