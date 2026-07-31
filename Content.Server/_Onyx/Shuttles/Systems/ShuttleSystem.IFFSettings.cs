using System.Linq;
using Content.Server.Shuttles.Components;
using Content.Shared.Shuttles.Events;
using Content.Shared.Database;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    private void OnIFFApplyRadarSettings(Entity<IFFConsoleComponent> ent, ref IFFApplyRadarSettingsMessage args)
    {
        var grid = Transform(ent).GridUid;
        if (grid == null || _station.GetOwningStation(grid) != null)
        {
            UpdateIFFInterface(ent);
            return;
        }

        var name = new string(args.Name.Trim().Where(c => !char.IsControl(c)).Take(64).ToArray());
        if (string.IsNullOrWhiteSpace(name))
        {
            UpdateIFFInterface(ent);
            return;
        }

        var color = args.Color.WithAlpha(1f);
        var metadata = MetaData(grid.Value);
        var iff = EnsureComp<IFFComponent>(grid.Value);
        if (metadata.EntityName == name && iff.Color == color)
            return;

        _metadata.SetEntityName(grid.Value, name, metadata);
        SetIFFColor(grid.Value, color, iff);
        _logger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.Actor):actor} changed IFF settings for {ToPrettyString(grid.Value):grid} to name '{name}' and color {color}.");
        _audio.PlayPvs("/Audio/Effects/Shuttle/radar_ping.ogg", ent);
    }

    private void UpdateIFFInterface(Entity<IFFConsoleComponent> ent)
    {
        var grid = Transform(ent).GridUid;
        _uiSystem.SetUiState(ent.Owner, IFFConsoleUiKey.Key,
            CreateIFFState(ent.Comp, grid, CompOrNull<IFFComponent>(grid)));
    }

    private IFFConsoleBoundUserInterfaceState CreateIFFState(
        IFFConsoleComponent console,
        EntityUid? grid = null,
        IFFComponent? iff = null)
    {
        return new IFFConsoleBoundUserInterfaceState
        {
            AllowedFlags = console.AllowedFlags,
            Flags = iff?.Flags ?? IFFFlags.None,
            Name = grid == null ? string.Empty : MetaData(grid.Value).EntityName,
            Color = iff?.Color ?? IFFComponent.IFFColor,
        };
    }
}
