using Content.Shared._Onyx.Body;
using Content.Shared._Onyx.Medical.Surgery;
using Content.Shared.Bed.Components;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Power.Components;
using Content.Server.Power.Components;
using Robust.Shared.Random;

namespace Content.Server._Onyx.Medical.Surgery;

public sealed partial class MissingHeartSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrganComponent, OrganGotRemovedEvent>(OnOrganChanged);
        SubscribeLocalEvent<OrganComponent, OrganGotInsertedEvent>(OnOrganChanged);
    }

    private void OnOrganChanged(Entity<OrganComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (TryComp(ent, out BodyPartComponent? part) && part.PartType == BodyPartType.Head)
            EnsureComp<MissingHeadComponent>(args.Target);

        switch (ent.Comp.Category?.Id)
        {
            case "Ears":
                EnsureComp<MissingEarsComponent>(args.Target);
                break;
            case "Lungs":
                EnsureComp<LungDependentComponent>(args.Target);
                break;
        }

        Refresh(args.Target);
    }

    private void OnOrganChanged(Entity<OrganComponent> ent, ref OrganGotInsertedEvent args)
    {
        if (TryComp(ent, out BodyPartComponent? part) && part.PartType == BodyPartType.Head)
            RemComp<MissingHeadComponent>(args.Target);

        switch (ent.Comp.Category?.Id)
        {
            case "Ears":
                RemComp<MissingEarsComponent>(args.Target);
                break;
            case "Lungs":
                EnsureComp<LungDependentComponent>(args.Target);
                break;
        }

        Refresh(args.Target);
    }

    private void Refresh(EntityUid body)
    {
        var hasHeartSlot = false;
        var hasHeart = false;
        foreach (var (part, component) in _body.GetBodyChildren(body))
        {
            if (!component.Organs.Contains("Heart"))
                continue;

            hasHeartSlot = true;
            hasHeart = _body.TryGetOrganInSlot(part, "Heart", out _);
            break;
        }

        if (!hasHeartSlot)
            return;

        if (hasHeart)
        {
            RemComp<MissingHeartComponent>(body);
            return;
        }

        if (HasComp<MissingHeartComponent>(body))
            return;

        var missing = EnsureComp<MissingHeartComponent>(body);
        missing.NormalDuration = _random.NextFloat(5f, 10f);
        missing.StasisDuration = _random.NextFloat(180f, 240f);
        Dirty(body, missing);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MissingHeartComponent>();
        while (query.MoveNext(out var uid, out var missing))
        {
            if (_mobState.IsDead(uid))
                continue;

            var duration = InPoweredStasis(uid) ? missing.StasisDuration : missing.NormalDuration;
            missing.Progress += frameTime / duration;
            if (missing.Progress < 1f)
                continue;

            _mobState.ChangeMobState(uid, MobState.Dead);
        }

        var headQuery = EntityQueryEnumerator<MissingHeadComponent>();
        while (headQuery.MoveNext(out var uid, out var missing))
        {
            if (_mobState.IsDead(uid))
                continue;

            missing.Elapsed += frameTime;
            if (missing.Elapsed >= 5f)
                _mobState.ChangeMobState(uid, MobState.Dead);
        }
    }

    private bool InPoweredStasis(EntityUid body)
    {
        return TryComp(body, out BuckleComponent? buckle) &&
               TryComp(buckle.BuckledTo, out StasisBedComponent? _) &&
               TryComp(buckle.BuckledTo, out ApcPowerReceiverComponent? power) &&
               power.Powered;
    }
}
