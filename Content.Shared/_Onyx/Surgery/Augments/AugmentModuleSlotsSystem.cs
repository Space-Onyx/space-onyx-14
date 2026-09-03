using Content.Shared.ActionBlocker;
using Content.Shared.Body;
using Content.Shared.CartridgeLoader;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.PDA;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.Surgery.Augments;

public sealed partial class AugmentModuleSlotsSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private AugmentModuleSystem _modules = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly VerbCategory AugmentationsCategory =
        new("augment-modules-verb-category", "/Textures/Interface/VerbIcons/group.svg.192dpi.png");
    private const float InteractionDelay = 2f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AugmentModuleSlotsComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<AugmentModuleSlotsComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<AugmentModuleSlotsComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<AugmentModuleSlotsComponent, ItemSlotEjectAttemptEvent>(OnEjectAttempt);
        SubscribeLocalEvent<AugmentModuleSlotsComponent, OrganGotInsertedEvent>(OnOrganInserted);
        SubscribeLocalEvent<AugmentModuleSlotsComponent, OrganGotRemovedEvent>(OnOrganRemoved);
        SubscribeLocalEvent<AugmentModuleSlotsComponent, AugmentModuleDetachedEvent>(OnDetached);
        SubscribeLocalEvent<AugmentModuleSlotsComponent, AugmentModuleInteractionDoAfterEvent>(OnInteractionDoAfter);
        SubscribeLocalEvent<AugmentModuleSlotsComponent, GetVerbsEvent<AlternativeVerb>>(OnAugmentVerbs);
        SubscribeLocalEvent<InstalledAugmentsComponent, GetVerbsEvent<AlternativeVerb>>(OnBodyVerbs);
    }

    private void OnInit(Entity<AugmentModuleSlotsComponent> ent, ref ComponentInit args)
    {
        var host = EnsureComp<AugmentModuleHostComponent>(ent);
        host.ManageThroughVerbs = false;

        foreach (var definition in ent.Comp.Slots)
        {
            if (string.IsNullOrWhiteSpace(definition.Id) || host.Slots.ContainsKey(definition.Id))
                continue;

            var slot = _itemSlots.AddAugmentModuleSlot((ent.Owner, null), definition.Id, definition.Name, definition.Whitelist);
            host.Slots.Add(definition.Id, slot);
        }
    }

    private void OnRemove(Entity<AugmentModuleSlotsComponent> ent, ref ComponentRemove args)
    {
        if (!TryComp(ent, out AugmentModuleHostComponent? host))
            return;

        foreach (var definition in ent.Comp.Slots)
        {
            if (!host.Slots.Remove(definition.Id, out var slot))
                continue;
            _itemSlots.RemoveItemSlot((ent.Owner, null), slot);
        }
    }

    private void OnInsertAttempt(Entity<AugmentModuleSlotsComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        var slotId = args.Slot.ContainerSlot?.ID;
        if (slotId == null || !TryGetDefinition(ent.Comp, slotId, out var definition))
            return;

        var installed = _modules.GetInstalledBody(ent) != null;
        if (installed ? !definition.AllowInsertWhenInstalled || !ent.Comp.PanelOpen : !definition.AllowInsertWhenUninstalled)
            args.Cancelled = true;
    }

    private void OnEjectAttempt(Entity<AugmentModuleSlotsComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        var slotId = args.Slot.ContainerSlot?.ID;
        if (slotId != null && TryGetDefinition(ent.Comp, slotId, out _) &&
            _modules.GetInstalledBody(ent) != null && !ent.Comp.PanelOpen)
            args.Cancelled = true;
    }

    private void OnOrganInserted(Entity<AugmentModuleSlotsComponent> ent, ref OrganGotInsertedEvent args) =>
        SetPanel(ent, args.Target, false);

    private void OnOrganRemoved(Entity<AugmentModuleSlotsComponent> ent, ref OrganGotRemovedEvent args)
    {
        SetPanel(ent, null, false);
        foreach (var module in _modules.GetModules(ent))
        {
            if (TryComp(module, out AugmentModuleSlotsComponent? slots))
                SetPanel((module, slots), null, false);
        }
    }

    private void OnDetached(Entity<AugmentModuleSlotsComponent> ent, ref AugmentModuleDetachedEvent args) =>
        SetPanel(ent, null, false);

    private void OnInteractionDoAfter(Entity<AugmentModuleSlotsComponent> ent, ref AugmentModuleInteractionDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } body || _modules.GetInstalledBody(ent) != body ||
            !CanStartInteraction(ent, args.User, args.Operation, args.SlotId, args.Open, args.Used))
            return;

        args.Handled = args.Operation switch
        {
            AugmentModuleInteraction.TogglePanel => TrySetPanel(ent, body, args.User, args.Open),
            AugmentModuleInteraction.InsertModule => TryInsert(ent, args.SlotId, args.User),
            AugmentModuleInteraction.EjectModule => TryEject(ent, args.SlotId, args.User),
            AugmentModuleInteraction.InsertCyberDeckItem => TryInsertCyberDeckItem(ent, args.SlotId, args.User),
            AugmentModuleInteraction.EjectCyberDeckItem => TryEjectCyberDeckItem(ent, args.SlotId, args.User),
            _ => false,
        };
    }

    private void OnAugmentVerbs(Entity<AugmentModuleSlotsComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null || _modules.GetInstalledBody(ent) != null)
            return;

        AddSlotVerbs(ent, ref args, installed: false);
    }

    private void OnBodyVerbs(Entity<InstalledAugmentsComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        foreach (var netAugment in ent.Comp.Augments)
        {
            var augment = GetEntity(netAugment);
            AddInstalledVerbs(augment, ent, ref args);

            foreach (var module in _modules.GetModules(augment))
                AddInstalledVerbs(module, ent, ref args);
        }
    }

    private void AddInstalledVerbs(
        EntityUid augment,
        Entity<InstalledAugmentsComponent> body,
        ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (HasComp<CyberDeckComponent>(augment))
            AddCyberDeckItemVerbs(augment, body, ref args);

        if (!TryComp(augment, out AugmentModuleSlotsComponent? slots))
            return;

        AddPanelVerb((augment, slots), body, ref args);

        if (slots.PanelOpen)
            AddSlotVerbs((augment, slots), ref args, installed: true);
    }

    private void AddCyberDeckItemVerbs(
        EntityUid cyberDeck,
        EntityUid body,
        ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (args.Using is { } held && _actionBlocker.CanDrop(args.User))
        {
            AddCyberDeckItemVerb(cyberDeck, body, PdaComponent.PdaIdSlotId, held, ref args);
            AddCyberDeckItemVerb(cyberDeck, body, CartridgeLoaderComponent.CartridgeSlotId, held, ref args);
        }

        AddCyberDeckEjectVerb(cyberDeck, body, PdaComponent.PdaIdSlotId, ref args);
        AddCyberDeckEjectVerb(cyberDeck, body, CartridgeLoaderComponent.CartridgeSlotId, ref args);
    }

    private void AddCyberDeckItemVerb(
        EntityUid cyberDeck,
        EntityUid body,
        string slotId,
        EntityUid held,
        ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!CanInsertCyberDeckItem(cyberDeck, slotId, held, args.User, out var slot))
            return;

        var user = args.User;
        var verb = new AlternativeVerb
        {
            Text = Loc.GetString("augment-modules-verb-insert-module-short",
                ("slot", slot.Name.Length == 0 ? Name(held) : Loc.GetString(slot.Name))),
            IconEntity = GetNetEntity(held),
            Act = () => TryStartInteraction(cyberDeck, body, user,
                AugmentModuleInteraction.InsertCyberDeckItem, slotId, used: held),
        };
        verb.SetCategoryPath(AugmentationsCategory, GetAugmentCategory(cyberDeck));
        args.Verbs.Add(verb);
    }

    private void AddCyberDeckEjectVerb(
        EntityUid cyberDeck,
        EntityUid body,
        string slotId,
        ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!CanEjectCyberDeckItem(cyberDeck, slotId, args.User, out var slot) || slot.Item is not { } item)
            return;

        var user = args.User;
        var verb = new AlternativeVerb
        {
            Text = Loc.GetString("augment-modules-verb-eject-module", ("module", Name(item))),
            IconEntity = GetNetEntity(item),
            Act = () => TryStartInteraction(cyberDeck, body, user,
                AugmentModuleInteraction.EjectCyberDeckItem, slotId),
        };
        verb.SetCategoryPath(AugmentationsCategory, GetAugmentCategory(cyberDeck));
        args.Verbs.Add(verb);
    }

    private void AddPanelVerb(
        Entity<AugmentModuleSlotsComponent> ent,
        EntityUid body,
        ref GetVerbsEvent<AlternativeVerb> args)
    {
        var open = !ent.Comp.PanelOpen;
        var user = args.User;
        var verb = new AlternativeVerb
        {
            Text = Loc.GetString(open
                ? "augment-modules-verb-open-panel"
                : "augment-modules-verb-close-panel"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
            Act = () => TryStartInteraction(ent, body, user, AugmentModuleInteraction.TogglePanel, open: open),
        };
        verb.SetCategoryPath(AugmentationsCategory, GetAugmentCategory(ent));
        args.Verbs.Add(verb);
    }

    private void AddSlotVerbs(
        Entity<AugmentModuleSlotsComponent> ent,
        ref GetVerbsEvent<AlternativeVerb> args,
        bool installed)
    {
        var user = args.User;
        var body = EntityUid.Invalid;
        if (installed)
        {
            if (_modules.GetInstalledBody(ent) is not { } installedBody)
                return;
            body = installedBody;
        }

        if (args.Using is { } heldModule)
        {
            foreach (var definition in ent.Comp.Slots)
            {
                var canInsert = installed
                    ? definition.AllowInsertWhenInstalled
                    : definition.AllowInsertWhenUninstalled;
                if (!definition.VisibleInVerbs || !canInsert ||
                    !_itemSlots.TryGetSlot((ent.Owner, null), definition.Id, out var slot) ||
                    !_itemSlots.CanInsert(ent.Owner, slot, heldModule, user))
                    continue;

                var verb = new AlternativeVerb
                {
                    Text = Loc.GetString("augment-modules-verb-insert-module-short", ("slot", Loc.GetString(definition.Name))),
                    Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/insert.svg.192dpi.png")),
                    Act = installed
                        ? () => TryStartInteraction(ent, body, user,
                            AugmentModuleInteraction.InsertModule, definition.Id, used: heldModule)
                        : () => TryInsert(ent.Owner, definition.Id, user),
                };
                verb.SetCategoryPath(AugmentationsCategory, GetAugmentCategory(ent));
                args.Verbs.Add(verb);
            }
        }

        foreach (var definition in ent.Comp.Slots)
        {
            if (!definition.VisibleInVerbs ||
                !_itemSlots.TryGetSlot((ent.Owner, null), definition.Id, out var slot) ||
                slot.Item is not { } installedModule ||
                !_itemSlots.CanEject(ent.Owner, slot, user))
                continue;

            var verb = new AlternativeVerb
            {
                Text = Loc.GetString("augment-modules-verb-eject-module", ("module", Name(installedModule))),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/eject.svg.192dpi.png")),
                Act = installed
                    ? () => TryStartInteraction(ent, body, user,
                        AugmentModuleInteraction.EjectModule, definition.Id)
                    : () => TryEject(ent.Owner, definition.Id, user),
            };
            verb.SetCategoryPath(AugmentationsCategory, GetAugmentCategory(ent));
            args.Verbs.Add(verb);
        }
    }

    public bool TrySetPanel(EntityUid augment, EntityUid body, EntityUid user, bool open)
    {
        if (_modules.GetInstalledBody(augment) != body || !TryComp(augment, out AugmentModuleSlotsComponent? slots))
            return false;

        SetPanel((augment, slots), body, open);
        return true;
    }

    public bool TryInsert(EntityUid augment, string slotId, EntityUid user)
    {
        if (!CanManage(augment, slotId, user, insert: true, out var slot))
            return false;
        return _itemSlots.TryInsertFromHand(augment, slot, user, excludeUserAudio: true);
    }

    public bool TryEject(EntityUid augment, string slotId, EntityUid user)
    {
        if (!CanManage(augment, slotId, user, insert: false, out var slot))
            return false;
        return _itemSlots.TryEjectToHands(augment, slot, user, excludeUserAudio: true);
    }

    public bool TryInsertCyberDeckItem(EntityUid cyberDeck, string slotId, EntityUid user)
    {
        if (!HasComp<CyberDeckComponent>(cyberDeck) || !IsCyberDeckItemSlot(slotId) ||
            _modules.GetInstalledBody(cyberDeck) == null ||
            !_itemSlots.TryGetSlot(cyberDeck, slotId, out var slot))
            return false;

        return _itemSlots.TryInsertFromHand(cyberDeck, slot, user, excludeUserAudio: true);
    }

    public bool TryEjectCyberDeckItem(EntityUid cyberDeck, string slotId, EntityUid user)
    {
        if (!CanEjectCyberDeckItem(cyberDeck, slotId, user, out var slot))
            return false;

        return _itemSlots.TryEjectToHands(cyberDeck, slot, user, excludeUserAudio: true);
    }

    private bool CanManage(EntityUid augment, string slotId, EntityUid user, bool insert, out ItemSlot slot)
    {
        slot = default!;
        if (!TryComp(augment, out AugmentModuleSlotsComponent? slots) ||
            !TryGetDefinition(slots, slotId, out var definition) ||
            !_itemSlots.TryGetSlot(augment, slotId, out var foundSlot))
            return false;

        slot = foundSlot;

        var body = _modules.GetInstalledBody(augment);
        if (body != null && !slots.PanelOpen)
            return false;

        return !insert || (body == null ? definition.AllowInsertWhenUninstalled : definition.AllowInsertWhenInstalled);
    }

    private bool CanInsertCyberDeckItem(
        EntityUid cyberDeck,
        string slotId,
        EntityUid held,
        EntityUid user,
        out ItemSlot slot)
    {
        slot = default!;
        if (!HasComp<CyberDeckComponent>(cyberDeck) || !IsCyberDeckItemSlot(slotId) ||
            _modules.GetInstalledBody(cyberDeck) == null ||
            !_itemSlots.TryGetSlot(cyberDeck, slotId, out var foundSlot) ||
            !_itemSlots.CanInsert(cyberDeck, foundSlot, held, user))
            return false;

        slot = foundSlot;
        return true;
    }

    private static bool IsCyberDeckItemSlot(string slotId) =>
        slotId is PdaComponent.PdaIdSlotId or CartridgeLoaderComponent.CartridgeSlotId;

    private bool CanEjectCyberDeckItem(EntityUid cyberDeck, string slotId, EntityUid user, out ItemSlot slot)
    {
        slot = default!;
        if (!HasComp<CyberDeckComponent>(cyberDeck) || !IsCyberDeckItemSlot(slotId) ||
            _modules.GetInstalledBody(cyberDeck) == null ||
            !_itemSlots.TryGetSlot(cyberDeck, slotId, out var foundSlot) ||
            !_itemSlots.CanEject(cyberDeck, foundSlot, user) || foundSlot.Item is not { } item ||
            !_actionBlocker.CanPickup(user, item))
            return false;

        slot = foundSlot;
        return true;
    }

    private bool TryStartInteraction(
        EntityUid augment,
        EntityUid body,
        EntityUid user,
        AugmentModuleInteraction operation,
        string slotId = "",
        bool open = false,
        EntityUid? used = null)
    {
        if (_modules.GetInstalledBody(augment) != body || !CanStartInteraction(augment, user, operation, slotId, open, used))
            return false;

        var doAfter = new DoAfterArgs(EntityManager, user, InteractionDelay,
            new AugmentModuleInteractionDoAfterEvent { Operation = operation, SlotId = slotId, Open = open },
            augment, target: body, used: used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };
        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        ShowInteractionPopup(augment, body, user, operation, open, used, slotId);

        return true;
    }

    private void ShowInteractionPopup(
        EntityUid augment,
        EntityUid body,
        EntityUid user,
        AugmentModuleInteraction operation,
        bool open,
        EntityUid? used,
        string slotId)
    {
        var action = operation switch
        {
            AugmentModuleInteraction.TogglePanel when open => "open-panel",
            AugmentModuleInteraction.TogglePanel => "close-panel",
            AugmentModuleInteraction.EjectModule or AugmentModuleInteraction.EjectCyberDeckItem => "eject",
            _ => "insert",
        };
        var item = used ?? GetSlotItem(augment, slotId) ?? augment;
        var key = user == body
            ? $"augment-modules-popup-{action}-self"
            : $"augment-modules-popup-{action}-other";
        var message = Loc.GetString(key,
            ("user", Identity.Entity(user, EntityManager)),
            ("target", Identity.Entity(body, EntityManager)),
            ("augment", augment),
            ("item", item));

        if (user == body)
            _popup.PopupEntity(message, body, body, PopupType.Medium);
        else
            _popup.PopupEntity(message, body, PopupType.Medium);
    }

    private EntityUid? GetSlotItem(EntityUid augment, string slotId) =>
        _itemSlots.TryGetSlot(augment, slotId, out var slot) ? slot.Item : null;

    private bool CanStartInteraction(
        EntityUid augment,
        EntityUid user,
        AugmentModuleInteraction operation,
        string slotId,
        bool open,
        EntityUid? used)
    {
        return operation switch
        {
            AugmentModuleInteraction.TogglePanel =>
                TryComp(augment, out AugmentModuleSlotsComponent? slots) && slots.PanelOpen != open,
            AugmentModuleInteraction.InsertModule => _actionBlocker.CanDrop(user) && used is { } module &&
                CanManage(augment, slotId, user, insert: true, out var insertSlot) &&
                _itemSlots.CanInsert(augment, insertSlot, module, user),
            AugmentModuleInteraction.EjectModule =>
                CanManage(augment, slotId, user, insert: false, out var ejectSlot) &&
                _itemSlots.CanEject(augment, ejectSlot, user) && ejectSlot.Item is { } ejected &&
                _actionBlocker.CanPickup(user, ejected),
            AugmentModuleInteraction.InsertCyberDeckItem => _actionBlocker.CanDrop(user) && used is { } item &&
                CanInsertCyberDeckItem(augment, slotId, item, user, out _),
            AugmentModuleInteraction.EjectCyberDeckItem =>
                CanEjectCyberDeckItem(augment, slotId, user, out _),
            _ => false,
        };
    }

    private void SetPanel(Entity<AugmentModuleSlotsComponent> ent, EntityUid? body, bool open)
    {
        if (ent.Comp.PanelOpen == open)
            return;

        ent.Comp.PanelOpen = open;
        Dirty(ent);

        var changed = new AugmentModulePanelStateChangedEvent(body, open);
        RaiseLocalEvent(ent, ref changed);
    }

    private static bool TryGetDefinition(
        AugmentModuleSlotsComponent slots,
        string slotId,
        out AugmentModuleSlotDefinition definition)
    {
        foreach (var candidate in slots.Slots)
        {
            if (candidate.Id != slotId)
                continue;

            definition = candidate;
            return true;
        }

        definition = default!;
        return false;
    }

    private VerbCategory GetAugmentCategory(EntityUid augment)
    {
        var prototype = MetaData(augment).EntityPrototype?.ID;
        return new(prototype == null ? "augment-modules-slot-default-name" : $"ent-{prototype}",
            "/Textures/Interface/VerbIcons/settings.svg.192dpi.png");
    }
}
