using System.Linq;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Whitelist;
using Content.Shared._Onyx.Cybernetics;
using Robust.Shared.Containers;

namespace Content.Shared._Onyx.Surgery.Augments;

public sealed partial class AugmentModuleSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AugmentModuleHostComponent, ComponentInit>(OnHostInit);
        SubscribeLocalEvent<AugmentModuleHostComponent, ComponentRemove>(OnHostRemove);
        SubscribeLocalEvent<AugmentModuleHostComponent, EntInsertedIntoContainerMessage>(OnModuleInserted);
        SubscribeLocalEvent<AugmentModuleHostComponent, EntRemovedFromContainerMessage>(OnModuleRemoved);
        SubscribeLocalEvent<InstalledAugmentsComponent, CyberneticsEmpProtectionEvent>(OnEmpProtection);
        SubscribeLocalEvent<AugmentModuleComponent, ExaminedEvent>(OnModuleExamined);
        SubscribeLocalEvent<AugmentModuleComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<AugmentModuleEmpProtectionComponent, CyberneticsEmpProtectionEvent>(OnModuleEmpProtection);
    }

    private void OnHostInit(Entity<AugmentModuleHostComponent> ent, ref ComponentInit args)
    {
        foreach (var (id, slot) in ent.Comp.Slots)
            _itemSlots.AddItemSlot((ent.Owner, null), id, slot);
    }

    private void OnHostRemove(Entity<AugmentModuleHostComponent> ent, ref ComponentRemove args)
    {
        foreach (var slot in ent.Comp.Slots.Values)
            _itemSlots.RemoveItemSlot((ent.Owner, null), slot);
    }

    private void OnInsertAttempt(Entity<AugmentModuleComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (ent.Owner == args.Item && _whitelist.IsWhitelistFail(ent.Comp.HostWhitelist, args.SlotEntity))
            args.Cancelled = true;
    }

    private void OnModuleInserted(Entity<AugmentModuleHostComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (HasComp<AugmentModuleComponent>(args.Entity))
            RaiseChanged(ent);
    }

    private void OnModuleRemoved(Entity<AugmentModuleHostComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (HasComp<AugmentModuleComponent>(args.Entity))
            RaiseChanged(ent);
    }

    private void OnEmpProtection(Entity<InstalledAugmentsComponent> ent, ref CyberneticsEmpProtectionEvent args)
    {
        RelayEvent(args.Cybernetic, ref args);
    }

    private void OnModuleEmpProtection(
        Entity<AugmentModuleEmpProtectionComponent> ent,
        ref CyberneticsEmpProtectionEvent args)
    {
        args.StrengthMultiplier *= SanitizeMultiplier(ent.Comp.StrengthMultiplier);
        args.DurationMultiplier *= SanitizeMultiplier(ent.Comp.DurationMultiplier);
    }

    private void OnModuleExamined(Entity<AugmentModuleComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("augment-module-examine"));
        if (TryComp(ent, out AugmentModuleEmpProtectionComponent? protection))
        {
            args.PushMarkup(Loc.GetString("augment-module-examine-emp-protection",
                ("strength", MathF.Round((1f - protection.StrengthMultiplier) * 100f)),
                ("duration", MathF.Round((1f - protection.DurationMultiplier) * 100f))));
        }
    }

    public IEnumerable<EntityUid> GetDirectModules(EntityUid host)
    {
        if (!TryComp(host, out AugmentModuleHostComponent? moduleHost))
            yield break;

        foreach (var (_, slot) in moduleHost.Slots
                     .OrderBy(entry => entry.Value.Priority)
                     .ThenBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (slot.Item is { } item && HasComp<AugmentModuleComponent>(item))
                yield return item;
        }
    }

    public IEnumerable<EntityUid> GetModules(EntityUid host)
    {
        var visited = new HashSet<EntityUid> { host };
        var pending = new Queue<EntityUid>();
        pending.Enqueue(host);
        while (pending.TryDequeue(out var current))
        {
            foreach (var module in GetDirectModules(current))
            {
                if (!visited.Add(module))
                    continue;

                yield return module;
                if (HasComp<AugmentModuleHostComponent>(module))
                    pending.Enqueue(module);
            }
        }
    }

    public void RelayEvent<T>(EntityUid host, ref T args) where T : notnull
    {
        foreach (var module in GetModules(host))
            RaiseLocalEvent(module, ref args);
    }

    private void RaiseChanged(EntityUid host)
    {
        var visited = new HashSet<EntityUid>();
        var current = host;
        while (visited.Add(current))
        {
            if (HasComp<AugmentModuleHostComponent>(current))
            {
                var changed = new AugmentModulesChangedEvent();
                RaiseLocalEvent(current, ref changed);
            }

            if (!_container.TryGetContainingContainer(current, out var parent))
                break;
            current = parent.Owner;
        }
    }

    private static float SanitizeMultiplier(float value) => float.IsFinite(value) ? Math.Max(0f, value) : 1f;
}
