using Content.Server._Onyx.Body;
using Content.Shared._Onyx.Body;
using Content.Shared._Onyx.Medical.Surgery;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;

namespace Content.Server._Onyx.Medical.Surgery;

public sealed partial class MissingHeartSystem : EntitySystem
{
    private const float NormalDuration = 30f;
    private const float StasisDuration = 300f;

    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyComponent, BodyOrgansChangedEvent>(OnBodyOrgansChanged);
    }

    private void OnBodyOrgansChanged(Entity<BodyComponent> ent, ref BodyOrgansChangedEvent args)
    {
        Refresh(ent);
    }

    private void Refresh(EntityUid body)
    {
        if (!_body.HasOrganSlot(body, "Heart"))
            return;

        var hasHeart = _body.HasOrgan(body, "Heart");

        if (hasHeart)
        {
            RemComp<MissingHeartComponent>(body);
            return;
        }

        if (HasComp<MissingHeartComponent>(body))
            return;

        var missing = EnsureComp<MissingHeartComponent>(body);
        missing.NormalDuration = NormalDuration;
        missing.StasisDuration = StasisDuration;
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

            var duration = BodyStasis.IsActive(EntityManager, uid) ? missing.StasisDuration : missing.NormalDuration;
            missing.Progress += frameTime / duration;
            if (missing.Progress < 1f)
                continue;

            _mobState.ChangeMobState(uid, MobState.Dead);
        }
    }
}