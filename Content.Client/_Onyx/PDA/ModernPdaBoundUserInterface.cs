using Content.Client.CartridgeLoader;
using Content.Client.PDA;
using Content.Client.UserInterface.Fragments;
using Content.Shared._Onyx.PDA;
using Content.Shared.CartridgeLoader;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.PDA;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._Onyx.PDA;

[UsedImplicitly]
public sealed class ModernPdaBoundUserInterface : CartridgeLoaderBoundUserInterface
{
    private readonly PdaSystem _pdaSystem;

    [ViewVariables]
    private ModernPdaMenu? _menu;

    private EntityUid? _attachedProgram;
    private UIFragment? _attachedUi;
    private Control? _attachedFragment;

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
        var modern = EntMan.GetComponentOrNull<PdaModernComponent>(Owner);
        _menu.SetSidebarBrand(modern?.SidebarBrand ?? "NT");
        _menu.SetupThemes(GetPdaThemes(modern));

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
        _menu.ProgramCloseButton.OnPressed += _ =>
        {
            if (_attachedProgram is { } program)
                SendMessage(new CartridgeLoaderUiMessage(EntMan.GetNetEntity(program), CartridgeUiMessageAction.Deactivate));
        };
        _menu.OnThemeChanged += accent => SendMessage(new PdaSetThemeMessage(accent)); // <Onyx-PdaTheme>

        var borderColor = EntMan.GetComponentOrNull<PdaBorderColorComponent>(Owner);
        if (borderColor == null)
            return;

        _menu.BorderColor = borderColor.BorderColor;
        _menu.AccentHColor = borderColor.AccentHColor;
        _menu.AccentVColor = borderColor.AccentVColor;
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);

        if (message is PdaBatteryUpdateMessage battery)
            _menu?.UpdateBatteryLevel(battery.Charge, battery.Max);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not CartridgeLoaderUiState loaderState)
        {
            _attachedUi?.UpdateState(state);
            return;
        }

        UpdateLoaderState(loaderState);
        _menu?.SetActiveProgram(_attachedProgram);

        if (state is not PdaUpdateState updateState)
            return;

        if (_menu == null)
        {
            _pdaSystem.Log.Error("PDA state received before menu was created.");
            return;
        }

        _menu.UpdateState(updateState);
    }

    private void UpdateLoaderState(CartridgeLoaderUiState loaderState)
    {
        var programs = new List<(EntityUid, CartridgeComponent)>();
        foreach (var programNet in loaderState.Programs)
        {
            if (!EntMan.TryGetEntity(programNet, out var programUid))
                continue;

            if (EntMan.GetComponentOrNull<CartridgeComponent>(programUid) is { } cartridge)
                programs.Add((programUid.Value, cartridge));
        }
        UpdateAvailablePrograms(programs);

        EntityUid? active = null;
        if (loaderState.ActiveUI is { } activeNet && EntMan.TryGetEntity(activeNet, out var activeUid))
            active = activeUid;

        if (active == _attachedProgram && _attachedFragment is { Disposed: false })
            return;

        DetachAttachedProgram();

        if (active is not { } program)
            return;

        if (EntMan.GetComponentOrNull<UIFragmentComponent>(program)?.Ui is not { } ui)
            return;

        ui.Setup(this, program);
        var control = ui.GetUIFragmentRoot();
        _attachedUi = ui;
        _attachedFragment = control;
        _attachedProgram = program;

        var activeCartridge = EntMan.GetComponentOrNull<CartridgeComponent>(program);
        AttachCartridgeUI(control, Loc.GetString(activeCartridge?.ProgramName ?? "default-program-name"));
        SendMessage(new CartridgeLoaderUiMessage(EntMan.GetNetEntity(program), CartridgeUiMessageAction.UIReady));
    }

    private void DetachAttachedProgram()
    {
        if (_attachedFragment is not null)
        {
            DetachCartridgeUI(_attachedFragment);
            _attachedFragment.Dispose();
        }

        _attachedFragment = null;
        _attachedUi = null;
        _attachedProgram = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _attachedFragment?.Dispose();
            _attachedFragment = null;
            _attachedUi = null;
            _attachedProgram = null;
        }

        base.Dispose(disposing);
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

    private List<(LocId LocKey, Color Color)> GetPdaThemes(PdaModernComponent? modern)
    {
        var themes = new List<(LocId, Color)>();
        if (modern == null)
            return themes;

        var protoMan = IoCManager.Resolve<IPrototypeManager>();
        foreach (var id in modern.ThemePresets)
        {
            if (protoMan.TryIndex(id, out PdaThemePrototype? theme))
                themes.Add((theme.Name, theme.Color));
        }

        return themes;
    }
}
