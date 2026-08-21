using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Network;

namespace Content.Shared._Onyx.Wounds;

/// <summary>
/// Drains blood directly from the bloodstream of a body while an internal bleeding
/// wound is active. Unlike regular wound bleeding, no blood leaks outside (no puddles).
/// </summary>
public sealed partial class WoundInternalBleedingSystem : EntitySystem
{
    [Dependency] private SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<WoundInternalBleedingComponent, WoundChangedEvent>(OnChanged);
        SubscribeLocalEvent<WoundInternalBleedingComponent, WoundStateChangedEvent>(OnStateChanged);
        SubscribeLocalEvent<WoundInternalBleedingComponent, WoundRemovedEvent>(OnRemoved);
    }

    private void OnChanged(Entity<WoundInternalBleedingComponent> ent, ref WoundChangedEvent args)
    {
        if (!_net.IsServer || !TryComp(ent, out WoundComponent? wound))
            return;

        ent.Comp.Severity = wound.State == WoundState.Open ? args.Severity : FixedPoint2.Zero;
        Dirty(ent);
    }

    private void OnStateChanged(Entity<WoundInternalBleedingComponent> ent, ref WoundStateChangedEvent args)
    {
        if (!_net.IsServer || !TryComp(ent, out WoundComponent? wound))
            return;

        ent.Comp.Severity = args.State == WoundState.Open ? wound.Severity : FixedPoint2.Zero;
        Dirty(ent);
    }

    private void OnRemoved(Entity<WoundInternalBleedingComponent> ent, ref WoundRemovedEvent args)
    {
        if (_net.IsServer)
        {
            ent.Comp.Severity = FixedPoint2.Zero;
            Dirty(ent);
        }
    }

    public override void Update(float frameTime)
    {
        if (!_net.IsServer)
            return;

        var query = EntityQueryEnumerator<WoundInternalBleedingComponent, WoundComponent>();
        while (query.MoveNext(out var uid, out var internalBleeding, out var core))
        {
            if (internalBleeding.Severity <= FixedPoint2.Zero || core.State != WoundState.Open)
                continue;

            if (TryGetBody(core.HoldingPart, out var body) && TryComp(body, out BloodstreamComponent? bloodstream))
            {
                var amount = FixedPoint2.New(internalBleeding.Rate * internalBleeding.Severity.Float() * frameTime);
                if (amount > FixedPoint2.Zero)
                    _bloodstream.TryModifyBloodLevel((body, bloodstream), -amount);
            }
        }
    }

    private bool TryGetBody(EntityUid part, out EntityUid body)
    {
        body = default;
        if (!TryComp(part, out BodyPartComponent? partComp) || partComp.Body is not { } attachedBody)
            return false;

        body = attachedBody;
        return true;
    }
}
