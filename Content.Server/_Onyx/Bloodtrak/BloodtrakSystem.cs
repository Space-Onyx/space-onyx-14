using System.Numerics;
using Content.Server.Forensics;
using Content.Shared._Onyx.Bloodtrak;
using Content.Shared.Fluids.Components;
using Content.Shared.Forensics.Components;
using Content.Shared.Interaction;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Timing;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Bloodtrak;

public sealed partial class BloodtrakSystem : SharedBloodtrakSystem
{
    private static readonly ProtoId<TagPrototype> DnaSolutionScannableTag = "DNASolutionScannable";

    [Dependency] private ForensicsSystem _forensics = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BloodtrakComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<BloodtrakComponent, ActivateInWorldEvent>(OnActivate);
    }

    private void OnAfterInteract(Entity<BloodtrakComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target is not { } target || ent.Comp.IsActive || _useDelay.IsDelayed(ent.Owner))
            return;

        args.Handled = true;
        if (!_tags.HasTag(target, DnaSolutionScannableTag) || !HasComp<PuddleComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("bloodtrak-scan-failed"), args.User, args.User);
            return;
        }

        if (ent.Comp.LastScannedTarget == target && ent.Comp.Results.Count > 0)
        {
            ent.Comp.ResultOffset = (ent.Comp.ResultOffset + 1) % ent.Comp.Results.Count;
            SelectResult(ent, args.User);
            return;
        }

        ent.Comp.LastScannedTarget = target;
        ent.Comp.ResultOffset = 0;
        ent.Comp.Results.Clear();
        if (!ent.Comp.FirstScanned.ContainsKey(target))
            ent.Comp.FirstScanned[target] = _timing.CurTime;

        var owners = GetDnaOwners();
        foreach (var dna in _forensics.GetSolutionsDNA(target))
        {
            if (owners.TryGetValue(dna, out var owner))
                ent.Comp.Results.Add((dna, owner));
        }

        if (ent.Comp.Results.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("bloodtrak-no-match"), args.User, args.User);
            ent.Comp.Target = null;
            return;
        }

        SelectResult(ent, args.User);
    }

    private void SelectResult(Entity<BloodtrakComponent> ent, EntityUid user)
    {
        var result = ent.Comp.Results[ent.Comp.ResultOffset];
        ent.Comp.Target = result.Owner;
        _popup.PopupEntity(Loc.GetString("bloodtrak-dna-saved", ("dna", result.Dna)), user, user);
        Dirty(ent);
    }

    private Dictionary<string, EntityUid> GetDnaOwners()
    {
        var owners = new Dictionary<string, EntityUid>();
        var query = EntityQueryEnumerator<DnaComponent>();
        while (query.MoveNext(out var uid, out var dna))
        {
            if (dna.DNA != null)
                owners[dna.DNA] = uid;
        }
        return owners;
    }

    private void OnActivate(Entity<BloodtrakComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex || _useDelay.IsDelayed(ent.Owner))
            return;

        args.Handled = true;
        if (ent.Comp.IsActive)
        {
            Deactivate(ent);
            return;
        }

        if (ent.Comp.Target is not { } target || !Exists(target))
        {
            _popup.PopupEntity(Loc.GetString("bloodtrak-no-target"), ent);
            return;
        }

        if (ent.Comp.LastScannedTarget is not { } puddle || !ent.Comp.FirstScanned.TryGetValue(puddle, out var freshness))
            return;

        ent.Comp.ExpirationTime = freshness + ent.Comp.MaximumTrackingDuration;
        if (ent.Comp.ExpirationTime <= _timing.CurTime)
        {
            _popup.PopupEntity(Loc.GetString("bloodtrak-sample-expired"), ent);
            return;
        }

        SetActive(ent, true);
        UpdateAppearance(ent);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<BloodtrakComponent>();
        while (query.MoveNext(out var uid, out var tracker))
        {
            if (!tracker.IsActive)
                continue;

            if (tracker.Target is not { } target || !Exists(target))
            {
                _popup.PopupEntity(Loc.GetString("bloodtrak-target-lost"), uid);
                Deactivate((uid, tracker));
                tracker.Target = null;
                _useDelay.SetLength(uid, tracker.CooldownDuration);
                continue;
            }

            if (_timing.CurTime >= tracker.ExpirationTime)
            {
                _popup.PopupEntity(Loc.GetString("bloodtrak-tracking-expired"), uid);
                Deactivate((uid, tracker));
                tracker.Target = null;
                _useDelay.SetLength(uid, tracker.CooldownDuration);
                continue;
            }

            UpdateDirection((uid, tracker), target);
        }
    }

    private void Deactivate(Entity<BloodtrakComponent> ent)
    {
        SetActive(ent, false);
        SetDistance(ent, BloodtrakDistance.Unknown);
        UpdateAppearance(ent);
    }

    private void UpdateDirection(Entity<BloodtrakComponent> ent, EntityUid target)
    {
        if (!TryComp(ent, out TransformComponent? sourceXform) || !TryComp(target, out TransformComponent? targetXform) ||
            sourceXform.MapID != targetXform.MapID)
        {
            SetDistance(ent, BloodtrakDistance.Unknown);
            UpdateAppearance(ent);
            return;
        }

        var vector = _transform.GetWorldPosition(targetXform) - _transform.GetWorldPosition(sourceXform);
        var angle = vector.ToWorldAngle();
        if (!ent.Comp.ArrowAngle.EqualsApprox(angle, ent.Comp.Precision))
        {
            ent.Comp.ArrowAngle = angle;
            Dirty(ent);
        }

        var length = vector.Length();
        var distance = length <= ent.Comp.ReachedDistance ? BloodtrakDistance.Reached
            : length <= ent.Comp.CloseDistance ? BloodtrakDistance.Close
            : length <= ent.Comp.MediumDistance ? BloodtrakDistance.Medium
            : length > ent.Comp.MaxDistance ? BloodtrakDistance.Unknown
            : BloodtrakDistance.Far;
        SetDistance(ent, distance);
        UpdateAppearance(ent);
    }

    private void UpdateAppearance(Entity<BloodtrakComponent> ent)
    {
        _appearance.SetData(ent, PinpointerVisuals.IsActive, ent.Comp.IsActive);
        _appearance.SetData(ent, PinpointerVisuals.TargetDistance, ent.Comp.DistanceToTarget);
    }
}
