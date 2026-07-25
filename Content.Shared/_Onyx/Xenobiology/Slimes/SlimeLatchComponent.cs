using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Onyx.Xenobiology.Slimes;

[RegisterComponent, NetworkedComponent]
public sealed partial class BeingLatchedComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid Slime;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class SlimeDigestingComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid Slime;

    [DataField]
    public FixedPoint2 SuctionUnits = 2.5;

    [DataField]
    public ProtoId<ReagentPrototype> ToxinReagent = "XenobioSlimeToxin";

    [DataField]
    public FixedPoint2 ToxinUnits = 0.15;

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextTick;

    [DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
        {
            { "Caustic", 2.5 },
        },
    };
}

public sealed partial class SlimeLatchActionEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class SlimeLatchDoAfterEvent : SimpleDoAfterEvent;
