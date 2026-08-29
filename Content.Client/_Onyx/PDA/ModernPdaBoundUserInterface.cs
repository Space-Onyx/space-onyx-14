using Content.Client.CartridgeLoader;
using Content.Client.PDA;
using Content.Client.UserInterface.Fragments;
using Content.Shared.CartridgeLoader;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.PDA;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Onyx.PDA;

[UsedImplicitly]
public sealed class ModernPdaBoundUserInterface : CartridgeLoaderBoundUserInterface
{
    private readonly PdaSystem _pdaSystem;

    [ViewVariables]
    private ModernPdaMenu? _menu;

    public ModernPdaBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _pdaSystem = EntMan.System<PdaSystem>();
    }

    protected override void Open()
    {
        base.Open();

        if (_menu == null)
            CreateMenu();
    }

    private void CreateMenu()
    {
        _menu = this.CreateWindowCenteredLeft<ModernPdaMenu>();

        _menu.FlashLightToggleButton.OnToggled += _ => SendMessage(new PdaToggleFlashlightMessage());
        _menu.EjectIdButton.OnPressed += _ =>
            SendMessage(new ItemSlotButtonPressedEvent(PdaComponent.PdaIdSlotId));
        _menu.EjectPenButton.OnPressed += _ =>
            SendMessage(new ItemSlotButtonPressedEvent(PdaComponent.PdaPenSlotId));
        _menu.EjectPaiButton.OnPressed += _ =>
            SendMessage(new ItemSlotButtonPressedEvent(PdaComponent.PdaPaiSlotId));
        _menu.PowerOffButton.OnPressed += _ => SendMessage(new PdaPowerOffMessage());
        _menu.ActivateMusicButton.OnPressed += _ => SendMessage(new PdaShowMusicMessage());
        _menu.AccessRingtoneButton.OnPressed += _ => SendMessage(new PdaShowRingtoneMessage());
        _menu.ShowUplinkButton.OnPressed += _ => SendMessage(new PdaShowUplinkMessage());
        _menu.LockUplinkButton.OnPressed += _ => SendMessage(new PdaLockUplinkMessage());

        _menu.OnProgramItemPressed += uid =>
        {
            if (EntMan.HasComponent<UIFragmentComponent>(uid))
                ActivateCartridge(uid);
        };
        _menu.OnInstallButtonPressed += InstallCartridge;
        _menu.OnUninstallButtonPressed += UninstallCartridge;
        _menu.ProgramCloseButton.OnPressed += _ => DeactivateActiveCartridge();
        _menu.OnThemeChanged += accent => SendMessage(new PdaSetThemeMessage(accent)); // <Onyx-PdaTheme>

        var borderColor = EntMan.GetComponentOrNull<PdaBorderColorComponent>(Owner);
        if (borderColor == null)
            return;

        _menu.BorderColor = borderColor.BorderColor;
        _menu.AccentHColor = borderColor.AccentHColor;
        _menu.AccentVColor = borderColor.AccentVColor;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not PdaUpdateState updateState)
            return;

        if (_menu == null)
        {
            _pdaSystem.Log.Error("PDA state received before menu was created.");
            return;
        }

        _menu.UpdateState(updateState);
    }

    protected override void AttachCartridgeUI(Control cartridgeUIFragment, string? title)
    {
        _menu?.ProgramView.AddChild(cartridgeUIFragment);
        _menu?.ToProgramView(title ?? Loc.GetString("comp-pda-io-program-fallback-title"));
    }

    protected override void DetachCartridgeUI(Control cartridgeUIFragment)
    {
        if (_menu == null)
            return;

        _menu.ToHomeScreen();
        _menu.HideProgramHeader();
        _menu.ProgramView.RemoveChild(cartridgeUIFragment);
    }

    protected override void UpdateAvailablePrograms(List<(EntityUid, CartridgeComponent)> programs)
    {
        _menu?.UpdateAvailablePrograms(programs);
    }
}
