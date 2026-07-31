using Content.Server.Shuttles.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Events;
using Robust.Shared.Audio;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    [Dependency] private EmagSystem _emag = default!;
    [Dependency] private SharedPopupSystem _iffPopup = default!;

    private void OnIFFShowVessel(Entity<IFFConsoleComponent> ent, ref IFFShowVesselMessage args)
    {
        var grid = Transform(ent).GridUid;
        if (grid == null || (ent.Comp.AllowedFlags & IFFFlags.Hide) == 0)
            return;

        if (RejectStationIFF(ent, grid.Value))
            return;

        if (args.Show)
            RemoveIFFFlag(grid.Value, IFFFlags.Hide);
        else
            AddIFFFlag(grid.Value, IFFFlags.Hide);
    }

    private void OnIFFEmagged(Entity<IFFConsoleComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction) ||
            _emag.CheckFlag(ent.Owner, EmagType.Interaction))
            return;

        ent.Comp.AllowedFlags |= IFFFlags.Hide | IFFFlags.HideLabel;
        UpdateIFFInterface(ent);
        args.Handled = true;
    }

    private bool RejectStationIFF(Entity<IFFConsoleComponent> console, EntityUid grid)
    {
        if (_station.GetOwningStation(grid) == null)
            return false;

        _iffPopup.PopupEntity(Loc.GetString("iff-console-station-iff-error"), console.Owner);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg"), console.Owner);
        UpdateIFFInterface(console);
        return true;
    }

    private bool EnsureStationIFFVisible(EntityUid grid)
    {
        if (_station.GetOwningStation(grid) == null)
            return false;

        RemoveIFFFlag(grid, IFFFlags.Hide | IFFFlags.HideLabel);
        return true;
    }
}
