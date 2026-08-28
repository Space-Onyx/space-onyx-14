using Content.Shared.Body.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared._Onyx.Wounds;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Chemistry.Circulation;

public sealed partial class CirculatoryStreamModifyBleedSystem : EntityEffectSystem<WoundHostComponent, CirculatoryStreamModifyBleed>
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private CirculatoryStreamSystem _circulation = default!;
    [Dependency] private WoundBleedingSystem _bleeding = default!;
    [Dependency] private WoundSystem _wounds = default!;

    protected override void Effect(Entity<WoundHostComponent> entity, ref EntityEffectEvent<CirculatoryStreamModifyBleed> args)
    {
        var amount = args.Effect.Amount * args.Scale;
        if (amount == 0f)
            return;

        if (amount > 0f)
        {
            foreach (var (part, _) in _body.GetBodyChildren(entity))
            {
                if (!TryComp(part, out WoundableComponent? woundable))
                    continue;
                if (_circulation.GetPartStream((part, woundable)) != args.Effect.Stream)
                    continue;

                if (_wounds.CanBleed(part) && _wounds.CanCreateWound(part, "SystemicBleedingWound"))
                {
                    _wounds.CreateOrMergeWound(part, "SystemicBleedingWound", FixedPoint2.New(amount));
                    return;
                }
            }

            return;
        }

        var wounds = new List<Entity<WoundComponent, WoundBleedingComponent>>();
        foreach (var (part, _) in _body.GetBodyChildren(entity))
        {
            if (!TryComp(part, out WoundableComponent? woundable))
                continue;
            if (_circulation.GetPartStream((part, woundable)) != args.Effect.Stream)
                continue;

            foreach (var wound in _wounds.GetWounds((part, woundable)))
            {
                if (TryComp(wound, out WoundBleedingComponent? bleeding) && bleeding.CurrentRate > 0f)
                    wounds.Add((wound, wound.Comp, bleeding));
            }
        }

        if (wounds.Count == 0)
            return;

        wounds.Sort((a, b) => b.Comp2.CurrentRate.CompareTo(a.Comp2.CurrentRate));

        var remaining = FixedPoint2.New(-amount);
        foreach (var wound in wounds)
        {
            var rate = FixedPoint2.New(wound.Comp2.CurrentRate);
            var denom = FixedPoint2.New(wound.Comp2.CurrentRate + wound.Comp2.NaturalClotting);
            if (denom == FixedPoint2.Zero)
                continue;

            var reduction = remaining >= rate
                ? wound.Comp2.BleedingSeverity
                : wound.Comp2.BleedingSeverity * remaining / denom;

            if (reduction <= FixedPoint2.Zero)
                continue;

            if (!TryComp(wound.Owner, out WoundComponent? core) || !TryComp(wound.Owner, out WoundBleedingComponent? bleeding))
                continue;

            bleeding.BleedingSeverity = FixedPoint2.Max(FixedPoint2.Zero, bleeding.BleedingSeverity - reduction);
            if (bleeding.BleedingSeverity == FixedPoint2.Zero)
            {
                RemComp<WoundBleedingComponent>(wound.Owner);
                _bleeding.RefreshBody(entity.Owner, core.HoldingPart);
            }
            else
            {
                bleeding.Treatment = BleedingTreatment.None;
                Dirty(wound.Owner, bleeding);
                _bleeding.RefreshBody(entity.Owner, core.HoldingPart);
            }

            remaining -= FixedPoint2.Min(remaining, rate);
            if (remaining <= FixedPoint2.Zero)
                break;
        }
    }
}

public sealed partial class CirculatoryStreamModifyBleed : EntityEffectBase<CirculatoryStreamModifyBleed>
{
    [DataField(required: true)]
    public ProtoId<CirculatoryStreamPrototype> Stream = default!;

    [DataField]
    public float Amount = -1f;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var streamName = prototype.TryIndex(Stream, out var proto) ? proto.ID : Stream.Id;
        return Loc.GetString("entity-effect-guidebook-circulatory-stream-modify-bleed",
            ("stream", streamName),
            ("amount", MathF.Abs(Amount)),
            ("sign", MathF.Sign(Amount)));
    }
}
