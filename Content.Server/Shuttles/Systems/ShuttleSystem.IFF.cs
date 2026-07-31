using Content.Server.Shuttles.Components;
using Content.Shared.CCVar;
using Content.Shared.Emag.Systems; // <Onyx-IFFVesselVisibility>
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Events;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    private void InitializeIFF()
    {
        SubscribeLocalEvent<IFFConsoleComponent, AnchorStateChangedEvent>(OnIFFConsoleAnchor);
        SubscribeLocalEvent<IFFConsoleComponent, IFFShowIFFMessage>(OnIFFShow);
        SubscribeLocalEvent<IFFConsoleComponent, IFFShowVesselMessage>(OnIFFShowVessel); // <Onyx-IFFVesselVisibility>
        SubscribeLocalEvent<IFFConsoleComponent, MapInitEvent>(OnInitIFFConsole);
        SubscribeLocalEvent<IFFConsoleComponent, IFFApplyRadarSettingsMessage>(OnIFFApplyRadarSettings); // <Onyx-IFFSettings>
        SubscribeLocalEvent<IFFConsoleComponent, GotEmaggedEvent>(OnIFFEmagged); // <Onyx-IFFVesselVisibility>
        SubscribeLocalEvent<GridSplitEvent>(OnGridSplit);
    }

    private void OnGridSplit(ref GridSplitEvent ev)
    {
        var splitMass = _cfg.GetCVar(CCVars.HideSplitGridsUnder);

        if (splitMass < 0)
            return;

        foreach (var grid in ev.NewGrids)
        {
            if (!_physicsQuery.TryGetComponent(grid, out var physics) ||
                physics.Mass > splitMass)
            {
                continue;
            }

            AddIFFFlag(grid, IFFFlags.HideLabel);
        }
    }

    private void OnIFFShow(EntityUid uid, IFFConsoleComponent component, IFFShowIFFMessage args)
    {
        if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid == null)
        {
            return;
        }

        if (RejectStationIFF((uid, component), xform.GridUid.Value)) // <Onyx-IFFVesselVisibility>
            return;

        if (!args.Show)
            AddIFFFlag(xform.GridUid.Value, IFFFlags.HideLabel); // <Onyx-IFFVesselVisibility-edited>
        else
            RemoveIFFFlag(xform.GridUid.Value, IFFFlags.HideLabel);
    }

    private void OnInitIFFConsole(EntityUid uid, IFFConsoleComponent component, MapInitEvent args)
    {
        if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid == null)
        {
            return;
        }

        if (EnsureStationIFFVisible(xform.GridUid.Value)) // <Onyx-StationIFFSafety>
            return;

        if (component.HideOnInit)
        {
            AddAllSupportedIFFFlags(xform, component);
        }
    }

    private void OnIFFConsoleAnchor(EntityUid uid, IFFConsoleComponent component, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored && TryComp(uid, out TransformComponent? anchoredXform) &&
            anchoredXform.GridUid is { } anchoredGrid)
            EnsureStationIFFVisible(anchoredGrid); // <Onyx-StationIFFSafety>

        // If we anchor / re-anchor then make sure flags up to date.
        if (!args.Anchored ||
            !TryComp(uid, out TransformComponent? xform) ||
            !TryComp<IFFComponent>(xform.GridUid, out var iff))
        {
            _uiSystem.SetUiState(uid, IFFConsoleUiKey.Key, new IFFConsoleBoundUserInterfaceState()
            {
                AllowedFlags = component.AllowedFlags,
                Flags = IFFFlags.None,
                Name = string.Empty, // <Onyx-IFFSettings>
                Color = IFFComponent.IFFColor, // <Onyx-IFFSettings>
            });
        }
        else
        {
            _uiSystem.SetUiState(uid, IFFConsoleUiKey.Key, new IFFConsoleBoundUserInterfaceState()
            {
                AllowedFlags = component.AllowedFlags,
                Flags = iff.Flags,
                Name = MetaData(xform.GridUid.Value).EntityName, // <Onyx-IFFSettings>
                Color = iff.Color, // <Onyx-IFFSettings>
            });
        }
    }

    protected override void UpdateIFFInterfaces(EntityUid gridUid, IFFComponent component)
    {
        base.UpdateIFFInterfaces(gridUid, component);

        var query = AllEntityQuery<IFFConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;

            _uiSystem.SetUiState(uid, IFFConsoleUiKey.Key, new IFFConsoleBoundUserInterfaceState()
            {
                AllowedFlags = comp.AllowedFlags,
                Flags = component.Flags,
                Name = MetaData(gridUid).EntityName, // <Onyx-IFFSettings>
                Color = component.Color, // <Onyx-IFFSettings>
            });
        }
    }

    // Made this method to avoid copy and pasting.
    /// <summary>
    /// Adds all IFF flags that are allowed by AllowedFlags to the grid.
    /// </summary>
    private void AddAllSupportedIFFFlags(TransformComponent xform, IFFConsoleComponent component)
    {
        if (xform.GridUid == null)
        {
            return;
        }

        if ((component.AllowedFlags & IFFFlags.HideLabel) != 0x0)
        {
            AddIFFFlag(xform.GridUid.Value, IFFFlags.HideLabel);
        }
        if ((component.AllowedFlags & IFFFlags.Hide) != 0x0)
        {
            AddIFFFlag(xform.GridUid.Value, IFFFlags.Hide);
        }
    }
}
