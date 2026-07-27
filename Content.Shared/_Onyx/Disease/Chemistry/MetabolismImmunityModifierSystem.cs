using Content.Shared._Onyx.Disease.Components;
using Content.Shared._Onyx.EntityEffects.Disease;
using Content.Shared.EntityEffects;
using Robust.Shared.Timing;

namespace Content.Shared._Onyx.Disease.Chemistry;

public sealed partial class MetabolismImmunityModifierSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ImmunityModifierMetabolismComponent, GetImmunityEvent>(OnGetImmunity);
    }

    private void OnGetImmunity(Entity<ImmunityModifierMetabolismComponent> ent, ref GetImmunityEvent args)
    {
        args.ImmunityGainRate += ent.Comp.GainRateModifier;
        args.ImmunityStrength += ent.Comp.StrengthModifier;
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImmunityModifierMetabolismComponent>();
        while (query.MoveNext(out var uid, out var modifier))
        {
            if (modifier.ModifierTimer > now)
                continue;

            RemCompDeferred<ImmunityModifierMetabolismComponent>(uid);
        }
    }
}

public sealed partial class ImmunityModifierEntityEffectSystem
    : EntityEffectSystem<ImmunityComponent, ImmunityModifier>
{
    [Dependency] private IGameTiming _timing = default!;

    protected override void Effect(Entity<ImmunityComponent> entity, ref EntityEffectEvent<ImmunityModifier> args)
    {
        var modifier = EnsureComp<ImmunityModifierMetabolismComponent>(entity);
        modifier.GainRateModifier = args.Effect.GainRateModifier;
        modifier.StrengthModifier = args.Effect.StrengthModifier;
        modifier.ModifierTimer = TimeSpan.FromSeconds(Math.Max(
            modifier.ModifierTimer.TotalSeconds,
            _timing.CurTime.TotalSeconds) + args.Effect.StatusLifetime * args.Scale);
        Dirty(entity.Owner, modifier);
    }
}
