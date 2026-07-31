using Content.Shared.FixedPoint;
using Content.Shared._Onyx.Xenomorphs.Caste;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Xenomorphs;

[Serializable, NetSerializable]
public sealed partial class XenomorphEvolutionDoAfterEvent : DoAfterEvent
{
    [DataField]
    public EntProtoId Choice;

    [DataField]
    public ProtoId<XenomorphCastePrototype> Caste;

    [DataField]
    public bool CheckNeedCasteDeath;

    [DataField]
    public NetEntity PlasmaPayer;

    [DataField]
    public FixedPoint2 PlasmaCost;

    public XenomorphEvolutionDoAfterEvent(EntProtoId choice, ProtoId<XenomorphCastePrototype> caste,
        NetEntity plasmaPayer, FixedPoint2 plasmaCost, bool checkNeedCasteDeath = true)
    {
        Choice = choice;
        Caste = caste;
        PlasmaPayer = plasmaPayer;
        PlasmaCost = plasmaCost;
        CheckNeedCasteDeath = checkNeedCasteDeath;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class LarvaBurstDoAfterEvent : SimpleDoAfterEvent;

public sealed partial class TransferPlasmaActionEvent : EntityTargetActionEvent
{
    [DataField]
    public FixedPoint2 Amount = 50;
}

public sealed partial class EvolutionsActionEvent : InstantActionEvent;

public sealed partial class PromotionActionEvent : EntityTargetActionEvent
{
    // Target is already provided by EntityTargetActionEvent
}

public sealed partial class TailLashActionEvent : WorldTargetActionEvent;

public sealed partial class AcidActionEvent : EntityTargetActionEvent;

public sealed partial class AfterXenomorphEvolutionEvent(EntityUid evolvedInto, EntityUid mindUid, ProtoId<XenomorphCastePrototype> caste) : EntityEventArgs
{
    public EntityUid EvolvedInto = evolvedInto;
    public EntityUid MindUid = mindUid;
    public ProtoId<XenomorphCastePrototype> Caste = caste;
}

public sealed partial class BeforeXenomorphEvolutionEvent(ProtoId<XenomorphCastePrototype> caste, bool checkNeedCasteDeath = true) : CancellableEntityEventArgs
{
    public ProtoId<XenomorphCastePrototype> Caste = caste;
    public bool CheckNeedCasteDeath = checkNeedCasteDeath;
}

public sealed partial class PlasmaAmountChangeEvent(FixedPoint2 amount) : EntityEventArgs
{
    public FixedPoint2 Amount = amount;
}
