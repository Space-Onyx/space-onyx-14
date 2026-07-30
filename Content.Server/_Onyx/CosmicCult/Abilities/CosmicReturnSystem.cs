using Content.Server._Onyx.CosmicCult.Components;
using Content.Shared._Onyx.CosmicCult;
using Content.Shared._Onyx.CosmicCult.Components;
using Content.Shared._Onyx.CosmicCult.Components.Examine;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mind;
using Content.Shared.Stunnable;
using Content.Server.Mind;
using Robust.Shared.Spawners;

namespace Content.Server._Onyx.CosmicCult.Abilities;

public sealed partial class CosmicReturnSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CosmicAstralBodyComponent, EventCosmicReturn>(OnCosmicReturn);
        SubscribeLocalEvent<CosmicAstralBodyComponent, EntityTerminatingEvent>(OnProjectionTerminating,
            before: [typeof(MindSystem)]);
        SubscribeLocalEvent<CosmicGlyphAstralProjectionComponent, TryActivateGlyphEvent>(OnAstralProjectionGlyph);
    }

    private void OnAstralProjectionGlyph(Entity<CosmicGlyphAstralProjectionComponent> uid, ref TryActivateGlyphEvent args)
    {
        _damageable.TryChangeDamage(args.User, uid.Comp.ProjectionDamage, true);
        var projectionEnt = Spawn(uid.Comp.SpawnProjection, Transform(uid).Coordinates);
        if (!_mind.TryGetMind(args.User, out var mindId, out var mind))
        {
            QueueDel(projectionEnt);
            return;
        }

        _mind.TransferTo(mindId, projectionEnt);
        EnsureComp<CosmicBlankComponent>(args.User);
        EnsureComp<CosmicAstralBodyComponent>(projectionEnt, out var astralComp);
        EnsureComp<TimedDespawnComponent>(projectionEnt).Lifetime = (float) uid.Comp.AstralDuration.TotalSeconds;
        mind.PreventGhosting = true;
        astralComp.OriginalBody = args.User;
        _stun.TryKnockdown(args.User, TimeSpan.FromSeconds(2), true);
    }

    /// <summary>
    ///     This action is exclusive to the Glyph-created Astral Projection, and allows the user to return to their original body.
    /// </summary>
    private void OnCosmicReturn(Entity<CosmicAstralBodyComponent> uid, ref EventCosmicReturn args)
    {
        ReturnMind(uid);
        QueueDel(uid);
    }

    private void OnProjectionTerminating(Entity<CosmicAstralBodyComponent> ent, ref EntityTerminatingEvent args)
    {
        ReturnMind(ent);
    }

    private void ReturnMind(Entity<CosmicAstralBodyComponent> ent)
    {
        if (!_mind.TryGetMind(ent, out var mindId, out var mind))
            return;

        mind.PreventGhosting = false;
        if (TerminatingOrDeleted(ent.Comp.OriginalBody))
            return;

        _mind.TransferTo(mindId, ent.Comp.OriginalBody);
        RemComp<CosmicBlankComponent>(ent.Comp.OriginalBody);
        RemComp<CosmicCultExamineComponent>(ent.Comp.OriginalBody);
    }
}
