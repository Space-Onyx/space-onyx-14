using Content.Shared._Onyx.Body;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Body;

/// <summary>
/// Generic organ pipeline: applies <see cref="OrganComponent.OnAdd"/> effects and missing-organ
/// consequences whenever an organ is inserted into or removed from a body.
/// </summary>
public sealed partial class OrganEffectSystem : EntitySystem
{
    private const float MissingHeadNormalDuration = 15f;
    private const float MissingHeadStasisDuration = 300f;

    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrganComponent, OrganGotRemovedEvent>(OnOrganRemoved);
        SubscribeLocalEvent<OrganComponent, OrganGotInsertedEvent>(OnOrganInserted);
        SubscribeLocalEvent<MissingEyesComponent, CanSeeAttemptEvent>(OnCanSee);
    }

    private void OnOrganRemoved(Entity<OrganComponent> ent, ref OrganGotRemovedEvent args)
    {
        ApplyOnAddComponents(ent, args.Target, remove: true);

        if (TryComp(ent, out BodyPartComponent? part) && part.PartType == BodyPartType.Head)
            EnsureComp<MissingHeadComponent>(args.Target);

        switch (ent.Comp.Category?.Id)
        {
            case "Eyes":
                EnsureComp<MissingEyesComponent>(args.Target);
                break;
            case "Ears":
                EnsureComp<MissingEarsComponent>(args.Target);
                break;
        }

        _blindable.UpdateIsBlind(args.Target);
        RaiseBodyOrgansChanged(args.Target);
    }

    private void OnOrganInserted(Entity<OrganComponent> ent, ref OrganGotInsertedEvent args)
    {
        ApplyOnAddComponents(ent, args.Target, remove: false);

        if (TryComp(ent, out BodyPartComponent? part) && part.PartType == BodyPartType.Head)
            RemComp<MissingHeadComponent>(args.Target);

        switch (ent.Comp.Category?.Id)
        {
            case "Eyes":
                RemComp<MissingEyesComponent>(args.Target);
                break;
            case "Ears":
                RemComp<MissingEarsComponent>(args.Target);
                break;
        }

        _blindable.UpdateIsBlind(args.Target);
        RaiseBodyOrgansChanged(args.Target);
    }

    private void RaiseBodyOrgansChanged(EntityUid body)
    {
        var ev = new BodyOrgansChangedEvent(body);
        RaiseLocalEvent(body, ref ev);
    }

    private void OnCanSee(Entity<MissingEyesComponent> ent, ref CanSeeAttemptEvent args)
    {
        args.Cancel();
    }

    private void ApplyOnAddComponents(Entity<OrganComponent> ent, EntityUid body, bool remove)
    {
        if (_timing.ApplyingState)
            return;

        if (ent.Comp.OnAdd is not { } onAdd)
            return;

        if (remove)
            EntityManager.RemoveComponents(body, onAdd);
        else
            EntityManager.AddComponents(body, onAdd);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var headQuery = EntityQueryEnumerator<MissingHeadComponent>();
        while (headQuery.MoveNext(out var uid, out var missing))
        {
            if (_mobState.IsDead(uid))
                continue;

            missing.Elapsed += frameTime;
            var duration = BodyStasis.IsActive(EntityManager, uid)
                ? MissingHeadStasisDuration
                : MissingHeadNormalDuration;
            if (missing.Elapsed >= duration)
                _mobState.ChangeMobState(uid, MobState.Dead);
        }
    }
}
