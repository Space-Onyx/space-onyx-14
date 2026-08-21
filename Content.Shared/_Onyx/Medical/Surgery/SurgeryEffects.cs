using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Medical.Surgery;

[RegisterComponent]
public sealed partial class SurgeryStepPainInflicterComponent : Component
{
    [DataField] public FixedPoint2 Amount = 5;
    [DataField] public FixedPoint2 SleepModifier = 1;
}

[RegisterComponent, NetworkedComponent] public sealed partial class BodyPartReattachedComponent : Component;
[RegisterComponent, NetworkedComponent] public sealed partial class BodyPartMendedComponent : Component;
[RegisterComponent, NetworkedComponent] public sealed partial class BodyPartSuturedComponent : Component;

[RegisterComponent]
public sealed partial class SurgeryStepBleedEffectComponent : Component
{
    [DataField] public int Damage;
}

[RegisterComponent] public sealed partial class SurgeryClampBleedEffectComponent : Component;

[RegisterComponent] public sealed partial class SurgeryCloseIncisionEffectComponent : Component;

[RegisterComponent] public sealed partial class SurgeryDetachPartEffectComponent : Component;

[RegisterComponent]
public sealed partial class SurgeryAttachPartEffectComponent : Component
{
    [DataField(required: true)] public BodyPartType Part;
    [DataField] public BodyPartSymmetry Symmetry;
}

[RegisterComponent]
public sealed partial class SurgeryMendAttachedPartEffectComponent : Component
{
    [DataField(required: true)] public BodyPartType Part;
    [DataField] public BodyPartSymmetry Symmetry;
}

[RegisterComponent]
public sealed partial class SurgerySutureAttachedPartEffectComponent : Component
{
    [DataField(required: true)] public BodyPartType Part;
    [DataField] public BodyPartSymmetry Symmetry;
}

[RegisterComponent]
public sealed partial class SurgeryOrganHealEffectComponent : Component
{
    [DataField(required: true)] public ProtoId<OrganCategoryPrototype> Slot;
    [DataField(required: true)] public FixedPoint2 Amount;
}

[RegisterComponent]
public sealed partial class SurgeryRemoveOrganEffectComponent : Component
{
    [DataField] public ProtoId<OrganCategoryPrototype>? Slot;
    [DataField] public ComponentRegistry Required = new();
}

[RegisterComponent]
public sealed partial class SurgeryInsertOrganEffectComponent : Component
{
    [DataField(required: true)] public ProtoId<OrganCategoryPrototype> Slot;
    [DataField] public bool RequireMechanical;
    [DataField] public ComponentRegistry? Required;
}

[RegisterComponent] public sealed partial class SurgeryInsertCavityItemEffectComponent : Component;
[RegisterComponent] public sealed partial class SurgeryRemoveCavityItemEffectComponent : Component;

/// <summary>Adds and removes components on the patient or operated body part.</summary>
[RegisterComponent]
public sealed partial class SurgeryComponentEffectComponent : Component
{
    [DataField]
    public SurgeryEntityTarget Target;

    [DataField]
    public ComponentRegistry Add = new();

    [DataField]
    public ComponentRegistry Remove = new();
}

[RegisterComponent] public sealed partial class SurgicallyPacifiedComponent : Component;

[RegisterComponent]
public sealed partial class SurgeryMutingEffectComponent : Component
{
    [DataField] public bool Remove;
}

[RegisterComponent]
public sealed partial class SurgeryStepEmoteEffectComponent : Component
{
    [DataField] public string Emote = "Scream";
}
