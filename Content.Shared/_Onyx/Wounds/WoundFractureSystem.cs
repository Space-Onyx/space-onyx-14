using System.Linq;
using Content.Shared.Body.Part;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Wounds;

public sealed partial class WoundFractureSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly ProtoId<WoundPrototype> FractureWound = "BoneFractureWound";

    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private WoundSystem _wounds = default!;

    public override void Initialize() =>
        SubscribeLocalEvent<WoundFractureComponent, WoundChangedEvent>(OnWoundChanged);

    internal void HandlePartDamageApplied(Entity<WoundableComponent> part, ref PartDamageAppliedEvent args)
    {
        if (!_net.IsServer || !args.Damage.DamageDict.TryGetValue(Blunt, out var blunt) || blunt == FixedPoint2.Zero ||
            !TryGetProfile(part.Owner, out var profile))
            return;

        var existing = GetFracture(part.Owner);
        if (blunt < FixedPoint2.Zero)
        {
            if (existing is { } healing)
                _wounds.ChangeSeverity(healing.Owner, blunt);
            return;
        }

        var hitGrade = GetGrade(profile, blunt);
        if (hitGrade == FractureGrade.None || existing is { } found && found.Comp2.Grade > hitGrade)
            return;

        if (existing is { } current)
        {
            var increase = FixedPoint2.Max(FixedPoint2.Zero, blunt - current.Comp2.BoneDamage);
            if (increase == FixedPoint2.Zero)
                return;

            ResetTreatment(current);
            SetBoneDamage(current, FixedPoint2.Max(current.Comp2.BoneDamage, blunt), profile);
            _wounds.ChangeSeverity(current.Owner, increase);
            return;
        }

        if (_wounds.CreateOrMergeWound(part.Owner, FractureWound, blunt) is not { } wound ||
            !TryComp(wound, out WoundComponent? core))
            return;

        var component = AddComp<WoundFractureComponent>(wound);
        SetBoneDamage((wound, core, component), blunt, profile);
    }

    private void OnWoundChanged(Entity<WoundFractureComponent> wound, ref WoundChangedEvent args)
    {
        if (!_net.IsServer || !TryComp(wound, out WoundComponent? core) ||
            !TryGetProfile(core.HoldingPart, out var profile))
            return;

        SetBoneDamage((wound, core, wound.Comp), FixedPoint2.Min(wound.Comp.BoneDamage, args.Severity), profile);
        if (wound.Comp.Grade == FractureGrade.None)
            _wounds.RemoveWound((wound.Owner, core));
    }

    public Entity<WoundComponent, WoundFractureComponent>? GetFracture(Entity<WoundableComponent?> part)
    {
        if (!Resolve(part, ref part.Comp, false))
            return null;

        foreach (var wound in _wounds.GetWounds(part))
            if (TryComp(wound, out WoundFractureComponent? fracture))
                return (wound, wound.Comp, fracture);
        return null;
    }

    public bool TryReduce(Entity<WoundComponent?> wound) => TrySetTreatment(wound, FractureTreatment.Reduced);
    public bool TryMend(Entity<WoundComponent?> wound) =>
        TrySetTreatment(wound, FractureTreatment.Mended) && _wounds.RemoveWound(wound);

    public bool TrySetTreatment(Entity<WoundComponent?> wound, FractureTreatment treatment)
    {
        if (!_net.IsServer || !Resolve(wound, ref wound.Comp, false) ||
            !TryComp(wound, out WoundFractureComponent? fracture) || fracture.Grade == FractureGrade.None)
            return false;

        var body = CompOrNull<BodyPartComponent>(wound.Comp.HoldingPart)?.Body;
        var attempt = new FractureTreatmentAttemptEvent(body, wound.Comp.HoldingPart, wound, treatment);
        RaiseLocalEvent(wound.Comp.HoldingPart, ref attempt);
        RaiseLocalEvent(wound, ref attempt);
        if (attempt.Cancelled || !CanTreat(fracture, treatment))
            return false;

        var old = fracture.Treatment;
        fracture.Treatment = treatment;
        Dirty(wound.Owner, fracture);
        var changed = new FractureTreatmentChangedEvent(body, wound.Comp.HoldingPart, wound, old, treatment);
        RaiseLocalEvent(wound.Comp.HoldingPart, ref changed);
        RaiseLocalEvent(wound, ref changed);
        return true;
    }

    public static FractureGrade GetGrade(FractureProfilePrototype profile, FixedPoint2 damage)
    {
        if (damage >= profile.ComminutedThreshold)
            return FractureGrade.Comminuted;
        if (damage >= profile.DisplacedThreshold)
            return FractureGrade.Displaced;
        if (damage >= profile.SimpleThreshold)
            return FractureGrade.Simple;
        return damage >= profile.HairlineThreshold ? FractureGrade.Hairline : FractureGrade.None;
    }

    public bool TryGetProfile(Entity<WoundableComponent?> part, out FractureProfilePrototype profile)
    {
        profile = default!;
        if (!Resolve(part, ref part.Comp, false) || !TryComp(part, out BodyPartComponent? bodyPart) ||
            !_prototypes.TryIndex(part.Comp.Profile, out var woundable))
            return false;

        var id = woundable.FractureProfiles.TryGetValue(bodyPart.PartType, out var partProfile)
            ? partProfile
            : woundable.FractureProfile;
        if (id is not { } profileId || !_prototypes.TryIndex(profileId, out var indexed))
            return false;
        profile = indexed;
        return true;
    }

    private void SetBoneDamage(Entity<WoundComponent, WoundFractureComponent> wound, FixedPoint2 damage,
        FractureProfilePrototype profile)
    {
        var oldDamage = wound.Comp2.BoneDamage;
        var oldGrade = wound.Comp2.Grade;
        var newDamage = FixedPoint2.Max(FixedPoint2.Zero, damage);
        var newGrade = GetGrade(profile, newDamage);
        if (oldDamage == newDamage && oldGrade == newGrade)
            return;

        wound.Comp2.BoneDamage = newDamage;
        wound.Comp2.Grade = newGrade;
        Dirty(wound.Owner, wound.Comp2);
        if (oldGrade == wound.Comp2.Grade)
            return;

        var body = CompOrNull<BodyPartComponent>(wound.Comp1.HoldingPart)?.Body;
        var changed = new FractureGradeChangedEvent(body, wound.Comp1.HoldingPart, wound, oldGrade, wound.Comp2.Grade);
        RaiseLocalEvent(wound.Comp1.HoldingPart, ref changed);
        RaiseLocalEvent(wound, ref changed);
    }

    private void ResetTreatment(Entity<WoundComponent, WoundFractureComponent> wound)
    {
        if (wound.Comp2.Treatment == FractureTreatment.None)
            return;

        var old = wound.Comp2.Treatment;
        wound.Comp2.Treatment = FractureTreatment.None;
        Dirty(wound.Owner, wound.Comp2);
        var body = CompOrNull<BodyPartComponent>(wound.Comp1.HoldingPart)?.Body;
        var changed = new FractureTreatmentChangedEvent(body, wound.Comp1.HoldingPart, wound, old, FractureTreatment.None);
        RaiseLocalEvent(wound.Comp1.HoldingPart, ref changed);
        RaiseLocalEvent(wound, ref changed);
    }

    private static bool CanTreat(WoundFractureComponent fracture, FractureTreatment treatment) => treatment switch
    {
        FractureTreatment.Reduced => fracture.Grade >= FractureGrade.Displaced && fracture.Treatment == FractureTreatment.None,
        FractureTreatment.Mended => fracture.Treatment != FractureTreatment.Mended &&
                                    (fracture.Grade < FractureGrade.Displaced || fracture.Treatment == FractureTreatment.Reduced),
        _ => false,
    };
}
