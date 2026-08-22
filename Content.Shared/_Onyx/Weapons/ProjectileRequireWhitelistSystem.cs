using Content.Shared.Clumsy.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Whitelist;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;

namespace Content.Shared._Onyx.Weapons;

public sealed partial class ProjectileRequireWhitelistSystem : EntitySystem
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ProjectileRequireWhitelistComponent, PreventCollideEvent>(OnCollide);
    }

    private void OnCollide(Entity<ProjectileRequireWhitelistComponent> ent, ref PreventCollideEvent args)
    {
        if (ent.Comp.Whitelist == null && !ent.Comp.RequireClumsy)
        {
            args.Cancelled = true;
            return;
        }

        var valid = ent.Comp.RequireClumsy
            ? _statusEffects.HasEffectComp<ClumsyCatchStatusEffectComponent>(args.OtherEntity)
            : _whitelist.IsValid(ent.Comp.Whitelist!, args.OtherEntity);
        if (valid != ent.Comp.Invert)
            return;
        if (ent.Comp.CollideWithWalls && args.OtherFixture.Hard && args.OtherBody.BodyType is BodyType.Static or BodyType.Dynamic)
            return;
        args.Cancelled = true;
    }
}
