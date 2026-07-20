using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Wounds;

public sealed partial class SuppressPainEntityEffectSystem : EntityEffectSystem<PainComponent, SuppressPain>
{
    [Dependency] private PainSystem _pain = default!;

    protected override void Effect(Entity<PainComponent> entity, ref EntityEffectEvent<SuppressPain> args)
    {
        _pain.SuppressPain((entity.Owner, entity.Comp), args.Effect.Identifier,
            args.Effect.Amount * args.Scale, args.Effect.DecayDuration);
    }
}

public sealed partial class SuppressPain : EntityEffectBase<SuppressPain>
{
    [DataField(required: true)]
    public FixedPoint2 Amount;

    [DataField(required: true)]
    public TimeSpan DecayDuration;

    [DataField]
    public string Identifier = "PainSuppressant";

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-suppress-pain",
            ("chance", Probability),
            ("amount", Amount.Float()),
            ("duration", DecayDuration.TotalSeconds));
}
