using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Projectiles;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Timing;

namespace Content.Shared._Onyx.Weapons;

public sealed partial class BlurOnCollideSystem : EntitySystem
{
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlurOnCollideComponent, ProjectileHitEvent>(OnHit);
        SubscribeLocalEvent<TemporaryBlurComponent, ComponentShutdown>(OnBlurShutdown);
    }

    private void OnHit(Entity<BlurOnCollideComponent> ent, ref ProjectileHitEvent args)
    {
        if (ent.Comp.BlurTime > TimeSpan.Zero)
        {
            var blur = EnsureComp<TemporaryBlurComponent>(args.Target);
            blur.EndTime = _timing.CurTime + ent.Comp.BlurTime;
            EnsureComp<BlurryVisionComponent>(args.Target);
            Dirty(args.Target, blur);
        }
        if (ent.Comp.BlindTime > TimeSpan.Zero)
            _status.TryAddStatusEffectDuration(args.Target, BlindnessSystem.BlindingStatusEffect, ent.Comp.BlindTime);
    }

    private void OnBlurShutdown(Entity<TemporaryBlurComponent> ent, ref ComponentShutdown args)
    {
        RemCompDeferred<BlurryVisionComponent>(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<TemporaryBlurComponent>();
        while (query.MoveNext(out var uid, out var blur))
        {
            if (_timing.CurTime >= blur.EndTime)
                RemCompDeferred<TemporaryBlurComponent>(uid);
        }
    }
}
