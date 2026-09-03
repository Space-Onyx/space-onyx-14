using System.Linq;
using Content.Shared.Access.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Content.Shared._Onyx.Cybernetics;
using Content.Shared.Body;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

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
        SubscribeLocalEvent<AugmentModuleHostComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<InstalledAugmentsComponent, GetAdditionalAccessEvent>(OnGetAdditionalAccess);
        SubscribeLocalEvent<AugmentModuleAccessProviderComponent, AccessibleOverrideEvent>(OnAccessible);
    }

    private void OnHostInit(Entity<AugmentModuleHostComponent> ent, ref ComponentInit args)
    {
        if (HasComp<AugmentModuleSlotsComponent>(ent))
            return;

        foreach (var (id, slot) in ent.Comp.Slots)
            _itemSlots.AddItemSlot((ent.Owner, null), id, slot);
    }

    private void OnHostRemove(Entity<AugmentModuleHostComponent> ent, ref ComponentRemove args)
    {
        if (HasComp<AugmentModuleSlotsComponent>(ent))
            return;

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
        {
            var detached = new AugmentModuleDetachedEvent(ent.Owner);
            RaiseLocalEvent(args.Entity, ref detached);
            RaiseChanged(ent);
        }
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

    private void OnGetVerbs(Entity<AugmentModuleHostComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!ent.Comp.ManageThroughVerbs || args.Hands == null || !args.CanAccess || !args.CanInteract ||
            GetInstalledBody(ent) is { } body && body != args.User)
            return;

        AddModuleVerbs(ent, ref args);
    }

    private void AddModuleVerbs(Entity<AugmentModuleHostComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!ent.Comp.ManageThroughVerbs)
            return;

        var user = args.User;

        if (TryComp(ent, out AugmentModuleServicePanelComponent? panel))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString(panel.Open
                    ? "augment-module-verb-close-slots"
                    : "augment-module-verb-open-slots"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
                Act = () => ToggleSlots(ent.Owner, user),
            });
            if (!panel.Open)
                return;
        }

        var category = new VerbCategory("augment-module-verb-category", null);
        if (!TryComp(ent, out ItemSlotsComponent? itemSlots))
            return;
        if (args.Using is { } item)
        {
            foreach (var (id, slot) in OrderedSlots(itemSlots))
            {
                if (slot.ShowVerbs)
                    continue;
                if (!_itemSlots.CanInsert(ent, slot, item, user))
                    continue;

                var verb = new AlternativeVerb
                {
                    Text = SlotName(slot, item),
                    IconEntity = GetNetEntity(item),
                    Priority = slot.Priority,
                    Act = () => TryInstallModule(ent.Owner, id, user),
                };
                verb.SetCategoryPath(VerbCategory.Insert, category);
                args.Verbs.Add(verb);
            }
            return;
        }

        foreach (var (id, slot) in OrderedSlots(itemSlots))
        {
            if (slot.ShowVerbs)
                continue;
            if (slot.DisableEject || !_itemSlots.CanEject(ent, slot, user) || slot.Item is not { } containedItem)
                continue;

            var verb = new AlternativeVerb
            {
                Text = SlotName(slot, containedItem),
                IconEntity = GetNetEntity(containedItem),
                Priority = slot.Priority,
                Act = () => TryRemoveModule(ent.Owner, id, user),
            };
            verb.SetCategoryPath(VerbCategory.Eject, category);
            args.Verbs.Add(verb);
        }
    }

    private void OnGetAdditionalAccess(Entity<InstalledAugmentsComponent> ent, ref GetAdditionalAccessEvent args)
    {
        foreach (var netAugment in ent.Comp.Augments)
        {
            var augment = GetEntity(netAugment);
            if (!Exists(augment))
                continue;

            foreach (var module in GetModules(augment))
            {
                if (HasComp<AugmentModuleAccessProviderComponent>(module))
                    args.Entities.Add(module);
            }
        }
    }

    private void OnAccessible(Entity<AugmentModuleAccessProviderComponent> ent, ref AccessibleOverrideEvent args)
    {
        if (GetInstalledBody(ent) != args.User)
            return;
        args.Handled = true;
        args.Accessible = true;
    }

    public EntityUid? GetInstalledBody(EntityUid entity)
    {
        var visited = new HashSet<EntityUid>();
        var current = entity;
        while (visited.Add(current))
        {
            if (TryComp(current, out OrganComponent? organ) && organ.Body is { } body)
                return body;
            if (!_container.TryGetContainingContainer(current, out var parent))
                return null;
            current = parent.Owner;
        }
        return null;
    }

    public bool TryInstallModule(EntityUid host, string slotId, EntityUid user)
    {
        if (!CanManage(host, slotId, user, out var slot))
            return false;
        return _itemSlots.TryInsertFromHand(host, slot, user, excludeUserAudio: true);
    }

    public bool TryRemoveModule(EntityUid host, string slotId, EntityUid user)
    {
        if (!CanManage(host, slotId, user, out var slot))
            return false;
        return _itemSlots.TryEjectToHands(host, slot, user, excludeUserAudio: true);
    }

    public bool ToggleSlots(EntityUid host, EntityUid user)
    {
        if (!TryComp(host, out AugmentModuleHostComponent? moduleHost) || !moduleHost.ManageThroughVerbs ||
            !TryComp(host, out AugmentModuleServicePanelComponent? panel) ||
            GetInstalledBody(host) is { } body && body != user)
            return false;
        panel.Open = !panel.Open;
        Dirty(host, panel);
        return true;
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

    private bool CanManage(EntityUid host, string slotId, EntityUid user, out ItemSlot slot)
    {
        slot = default!;
        if (!TryComp(host, out AugmentModuleHostComponent? moduleHost) || !moduleHost.ManageThroughVerbs ||
            TryComp(host, out AugmentModuleServicePanelComponent? panel) && !panel.Open ||
            GetInstalledBody(host) is { } body && body != user ||
            !_itemSlots.TryGetSlot(host, slotId, out var foundSlot) || foundSlot.ShowVerbs)
            return false;

        slot = foundSlot;
        return true;
    }

    private static IEnumerable<KeyValuePair<string, ItemSlot>> OrderedSlots(ItemSlotsComponent slots) =>
        slots.Slots.OrderBy(entry => entry.Value.Priority).ThenBy(entry => entry.Key, StringComparer.Ordinal);

    private string SlotName(ItemSlot slot, EntityUid item) =>
        slot.Name.Length == 0 ? Name(item) : Loc.GetString(slot.Name);

    private static float SanitizeMultiplier(float value) => float.IsFinite(value) ? Math.Max(0f, value) : 1f;
}
