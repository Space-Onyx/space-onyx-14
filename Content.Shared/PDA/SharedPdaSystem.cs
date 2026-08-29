using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared._Onyx.PDA; // <Onyx-PdaScreenVisuals>
using Content.Shared.CartridgeLoader; // <Onyx-PdaScreenVisuals>
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Containers;
using Robust.Shared.Utility; // <Onyx-PdaScreenVisuals>

namespace Content.Shared.PDA
{
    public abstract partial class SharedPdaSystem : EntitySystem
    {
        [Dependency] protected ItemSlotsSystem ItemSlotsSystem = default!;
        [Dependency] protected SharedAppearanceSystem Appearance = default!;
        [Dependency] private SharedJobStatusSystem _jobStatus = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<PdaComponent, ComponentInit>(OnComponentInit);
            SubscribeLocalEvent<PdaComponent, ComponentRemove>(OnComponentRemove);

            SubscribeLocalEvent<PdaComponent, EntInsertedIntoContainerMessage>(OnItemInserted);
            SubscribeLocalEvent<PdaComponent, EntRemovedFromContainerMessage>(OnItemRemoved);

            SubscribeLocalEvent<PdaComponent, GetAdditionalAccessEvent>(OnGetAdditionalAccess);
            // <Onyx-PdaScreenVisuals>
            SubscribeLocalEvent<CartridgeComponent, CartridgeActivatedEvent>(OnCartridgeActivated);
            SubscribeLocalEvent<CartridgeComponent, CartridgeDeactivatedEvent>(OnCartridgeDeactivated);
            // </Onyx-PdaScreenVisuals>
        }
        protected virtual void OnComponentInit(EntityUid uid, PdaComponent pda, ComponentInit args)
        {
            if (pda.IdCard != null)
                pda.IdSlot.StartingItem = pda.IdCard;

            ItemSlotsSystem.AddItemSlot(uid, PdaComponent.PdaIdSlotId, pda.IdSlot);
            ItemSlotsSystem.AddItemSlot(uid, PdaComponent.PdaPenSlotId, pda.PenSlot);
            ItemSlotsSystem.AddItemSlot(uid, PdaComponent.PdaPaiSlotId, pda.PaiSlot);

            UpdatePdaAppearance(uid, pda);
            // <Onyx-PdaScreenVisuals>
            if (TryGetPdaScreen(uid, false, out var screen))
                Appearance.SetData(uid, PdaVisuals.ScreenState, screen);
            // </Onyx-PdaScreenVisuals>
        }

        private void OnComponentRemove(EntityUid uid, PdaComponent pda, ComponentRemove args)
        {
            ItemSlotsSystem.RemoveItemSlot(uid, pda.IdSlot);
            ItemSlotsSystem.RemoveItemSlot(uid, pda.PenSlot);
            ItemSlotsSystem.RemoveItemSlot(uid, pda.PaiSlot);
        }

        protected virtual void OnItemInserted(EntityUid uid, PdaComponent pda, EntInsertedIntoContainerMessage args)
        {
            if (args.Container.ID == PdaComponent.PdaIdSlotId)
                pda.ContainedId = args.Entity;

            UpdatePdaAppearance(uid, pda);
            UpdateJobStatus(uid);
        }

        protected virtual void OnItemRemoved(EntityUid uid, PdaComponent pda, EntRemovedFromContainerMessage args)
        {
            if (args.Container.ID == pda.IdSlot.ID)
                pda.ContainedId = null;

            UpdatePdaAppearance(uid, pda);
            UpdateJobStatus(uid);
        }

        private void OnGetAdditionalAccess(EntityUid uid, PdaComponent component, ref GetAdditionalAccessEvent args)
        {
            if (component.ContainedId is { } id)
                args.Entities.Add(id);
        }

        private void UpdatePdaAppearance(EntityUid uid, PdaComponent pda)
        {
            Appearance.SetData(uid, PdaVisuals.IdCardInserted, pda.ContainedId != null);
        }

        // <Onyx-PdaScreenVisuals>
        private void OnCartridgeActivated(Entity<CartridgeComponent> cartridge, ref CartridgeActivatedEvent args)
        {
            if (!TryComp<PdaScreenVisualsComponent>(args.Loader.Owner, out var visuals))
                return;

            Appearance.SetData(args.Loader.Owner, PdaVisuals.ScreenState, cartridge.Comp.ScreenState ?? visuals.IdleScreen);
        }

        private void OnCartridgeDeactivated(Entity<CartridgeComponent> cartridge, ref CartridgeDeactivatedEvent args)
        {
            if (TryComp<PdaScreenVisualsComponent>(args.Loader.Owner, out var visuals))
                Appearance.SetData(args.Loader.Owner, PdaVisuals.ScreenState, visuals.MenuScreen);
        }

        protected bool TryGetPdaScreen(EntityUid uid, bool showMenu, out SpriteSpecifier screen)
        {
            screen = default!;
            if (!TryComp<PdaScreenVisualsComponent>(uid, out var visuals))
                return false;

            if (TryComp<CartridgeLoaderComponent>(uid, out var loader) &&
                loader.ActiveProgram is { } active &&
                TryComp<CartridgeComponent>(active, out var cartridge) &&
                cartridge.ScreenState is { } programScreen)
            {
                screen = programScreen;
                return true;
            }

            screen = showMenu ? visuals.MenuScreen : visuals.IdleScreen;
            return true;
        }
        // </Onyx-PdaScreenVisuals>

        // update the status icon of the player that has the pda currently equipped
        private void UpdateJobStatus(EntityUid uid)
        {
            // Only the player who has the pda currently equipped can insert or remove Ids
            var parent = Transform(uid).ParentUid;
            _jobStatus.UpdateStatus(parent);
        }

        public virtual void UpdatePdaUi(EntityUid uid, PdaComponent? pda = null)
        {
            // This does nothing yet while I finish up PDA prediction
            // Overriden by the server
        }
    }
}
