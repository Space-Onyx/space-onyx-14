using System.Linq;
using Content.Shared.Access.Systems;
using Content.Shared.Contraband;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Onyx.Contraband;

public abstract partial class SharedContrabandDetectorSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedIdCardSystem _idCards = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ContrabandDetectorComponent, PowerChangedEvent>(OnPowerChanged);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ContrabandDetectorComponent>();
        while (query.MoveNext(out var uid, out var detector))
        {
            if (detector.State != ContrabandDetectorState.Powered &&
                detector.LastScanTime + detector.ScanTimeOut < _timing.CurTime &&
                _power.IsPowered(uid))
            {
                detector.State = ContrabandDetectorState.Powered;
                UpdateVisuals((uid, detector));
                Dirty(uid, detector);
            }

            foreach (var scanned in detector.Scanned.Where(entry => _timing.CurTime > entry.Value).ToArray())
                detector.Scanned.Remove(scanned.Key);
        }
    }

    public List<EntityUid> FindContraband(EntityUid uid)
    {
        var items = new HashSet<EntityUid> { uid };
        FindStored(uid, items);

        var slots = _inventory.GetSlotEnumerator(uid, SlotFlags.All);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is not { } item)
                continue;
            items.Add(item);
            FindStored(item, items);
        }

        foreach (var item in _hands.EnumerateHeld(uid))
        {
            items.Add(item);
            FindStored(item, items);
        }

        return items.Where(item => HasComp<ContrabandComponent>(item) &&
            !HasComp<UndetectableContrabandComponent>(item) &&
            !HasPermission(item, uid)).ToList();
    }

    protected void UpdateVisuals(Entity<ContrabandDetectorComponent> detector)
    {
        _appearance.SetData(detector, ContrabandDetectorVisuals.VisualState, detector.Comp.State);
    }

    public void ChangeFalseDetectionChance(Entity<ContrabandDetectorComponent> detector)
    {
        detector.Comp.FalseDetectingChance = detector.Comp.IsFalseDetectingChanged
            ? detector.Comp.FalseDetectingChance / detector.Comp.FalseDetectingChanceMultiplier
            : detector.Comp.FalseDetectingChance * detector.Comp.FalseDetectingChanceMultiplier;
        detector.Comp.IsFalseDetectingChanged = !detector.Comp.IsFalseDetectingChanged;
        Dirty(detector);
    }

    public void ToggleFakeScanning(Entity<ContrabandDetectorComponent> detector)
    {
        detector.Comp.IsFalseScanning = !detector.Comp.IsFalseScanning;
        Dirty(detector);
    }

    private void FindStored(EntityUid uid, HashSet<EntityUid> items)
    {
        if (!TryComp(uid, out StorageComponent? storage) || HasComp<HideContrabandContentComponent>(uid))
            return;

        foreach (var item in storage.Container.ContainedEntities)
        {
            if (items.Add(item))
                FindStored(item, items);
        }
    }

    private bool HasPermission(EntityUid contraband, EntityUid user)
    {
        if (!TryComp(contraband, out ContrabandComponent? component))
            return true;

        var jobs = component.AllowedJobs.Select(id => _prototypes.Index(id).LocalizedName);
        if (!_idCards.TryFindIdCard(user, out var id))
            return false;

        return id.Comp.JobDepartments.Intersect(component.AllowedDepartments).Any() ||
               id.Comp.LocalizedJobTitle != null && jobs.Contains(id.Comp.LocalizedJobTitle);
    }

    private void OnPowerChanged(Entity<ContrabandDetectorComponent> detector, ref PowerChangedEvent args)
    {
        detector.Comp.State = args.Powered ? ContrabandDetectorState.Powered : ContrabandDetectorState.Off;
        UpdateVisuals(detector);
        Dirty(detector);
    }
}
