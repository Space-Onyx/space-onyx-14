// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Linq;
using Content.Server.Research.Systems;
using Content.Shared._Onyx.Research;
using Content.Shared._Onyx.Research.Components;
using Content.Shared._Onyx.Research.Prototypes;
using Content.Shared.Item;
using Content.Shared.Research.Components;
using Content.Shared.SubFloor;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Research.Systems;

public sealed partial class ResearchExperimentMachineSystem : EntitySystem
{
    [Dependency] private ResearchSystem _research = default!;
    [Dependency] private ResearchExperimentScannerSystem _scanner = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private AudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ResearchExperimentMachineComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ResearchExperimentMachineComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ResearchExperimentMachineComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<ResearchExperimentMachineComponent, OpenExperimentServerMenuMessage>(OnOpenServerMenu);
        SubscribeLocalEvent<ResearchExperimentMachineComponent, RunResearchExperimentMessage>(OnRun);
        SubscribeLocalEvent<ResearchExperimentMachineComponent, ResearchRegistrationChangedEvent>(OnRegistrationChanged);
        SubscribeLocalEvent<ResearchExperimentMachineComponent, ResearchServerPointTypeChangedEvent>(OnPointsChanged);
    }

    private void OnStartup(Entity<ResearchExperimentMachineComponent> ent, ref ComponentStartup args)
    {
        _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);
        SetState(ent, ResearchExperimentMachineState.Idle);
    }

    private void OnShutdown(Entity<ResearchExperimentMachineComponent> ent, ref ComponentShutdown args) => ReleaseSamples(ent);
    private void OnUiOpened(Entity<ResearchExperimentMachineComponent> ent, ref BoundUIOpenedEvent args) => UpdateUi(ent);
    private void OnRegistrationChanged(Entity<ResearchExperimentMachineComponent> ent, ref ResearchRegistrationChangedEvent args) => UpdateUi(ent);
    private void OnPointsChanged(Entity<ResearchExperimentMachineComponent> ent, ref ResearchServerPointTypeChangedEvent args) => UpdateUi(ent);
    private void OnOpenServerMenu(Entity<ResearchExperimentMachineComponent> ent, ref OpenExperimentServerMenuMessage args) =>
        _ui.TryToggleUi(ent.Owner, ResearchClientUiKey.Key, args.Actor);

    private void OnRun(Entity<ResearchExperimentMachineComponent> ent, ref RunResearchExperimentMessage args)
    {
        if (ent.Comp.Processing || !_research.TryGetClientServer(ent, out var server, out _))
        {
            Fail(ent, ent.Comp.Processing ? "research-experiment-machine-busy" : "research-experiment-scanner-no-server");
            return;
        }

        var xform = Transform(ent);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp) ||
            !_map.TryGetTileRef(grid, gridComp, xform.Coordinates, out var tile))
        {
            Fail(ent, "research-experiment-machine-no-samples");
            return;
        }

        var storage = _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);
        var samples = _lookup.GetLocalEntitiesIntersecting(tile, 0f)
            .Where(uid => uid != ent.Owner && HasComp<ItemComponent>(uid) &&
                          !HasComp<ResearchClientComponent>(uid) && !_container.TryGetContainingContainer(uid, out _) &&
                          !Transform(uid).Anchored && !IsUnderCover(uid))
            .Distinct()
            .Where(uid => _container.Insert(uid, storage))
            .ToList();
        if (samples.Count == 0)
        {
            Fail(ent, "research-experiment-machine-no-samples");
            return;
        }

        ent.Comp.Processing = true;
        ent.Comp.LastSubject = string.Join(", ", samples.Select(uid => Name(uid)));
        ent.Comp.LastResult = Loc.GetString("research-experiment-machine-processing", ("count", samples.Count));
        SetState(ent, ResearchExperimentMachineState.Closing);
        UpdateUi(ent);

        var actor = args.Actor;
        Timer.Spawn(ent.Comp.AnimationDuration, () =>
        {
            if (!TerminatingOrDeleted(ent) && ent.Comp.Processing)
                SetState(ent, ResearchExperimentMachineState.Scanning);
        });
        Timer.Spawn(ent.Comp.ScanDuration, () => Complete(ent, server.Value, samples, actor));
    }

    private void Complete(Entity<ResearchExperimentMachineComponent> ent, EntityUid server, List<EntityUid> samples, EntityUid user)
    {
        if (TerminatingOrDeleted(ent))
            return;

        var changed = false;
        var completed = new HashSet<string>();
        foreach (var sample in samples)
        {
            if (TerminatingOrDeleted(sample) || !_research.TryProgressExperiment(server,
                    sample,
                    user,
                    ExperimentSource.MachineScanner,
                    out var sampleChanged,
                    out var sampleCompleted,
                    out _))
                continue;
            changed |= sampleChanged;
            completed.UnionWith(sampleCompleted.Select(id => id.ToString()));
        }

        ent.Comp.Processing = false;
        ent.Comp.LastResult = completed.Count > 0
            ? Loc.GetString("research-experiment-machine-completed", ("count", completed.Count))
            : Loc.GetString(changed ? "research-experiment-machine-progressed" : "research-experiment-scanner-no-match");
        _audio.PlayPvs(changed ? ent.Comp.SuccessSound : ent.Comp.FailureSound, ent);
        SetState(ent, ResearchExperimentMachineState.Opening);
        Timer.Spawn(ent.Comp.AnimationDuration, () =>
        {
            if (TerminatingOrDeleted(ent) || ent.Comp.Processing)
                return;
            ReleaseSamples(ent);
            SetState(ent, ResearchExperimentMachineState.Idle);
            UpdateUi(ent);
        });
        UpdateUi(ent);
    }

    private void ReleaseSamples(Entity<ResearchExperimentMachineComponent> ent)
    {
        if (_container.TryGetContainer(ent, ent.Comp.ContainerId, out var storage))
            _container.EmptyContainer(storage, true, Transform(ent).Coordinates);
    }

    private void Fail(Entity<ResearchExperimentMachineComponent> ent, string message)
    {
        ent.Comp.LastResult = Loc.GetString(message);
        _audio.PlayPvs(ent.Comp.FailureSound, ent);
        UpdateUi(ent);
    }

    private void SetState(Entity<ResearchExperimentMachineComponent> ent, ResearchExperimentMachineState state) =>
        _appearance.SetData(ent, ResearchExperimentMachineVisuals.State, state);

    private bool IsUnderCover(EntityUid uid) =>
        TryComp<SubFloorHideComponent>(uid, out var hide) && hide.IsUnderCover;

    private void UpdateUi(Entity<ResearchExperimentMachineComponent> ent)
    {
        string? serverName = null;
        var balances = new List<ResearchPointAmount>();
        var experiments = new List<ResearchExperimentUiEntry>();
        if (_research.TryGetClientServer(ent, out var serverUid, out var server) &&
            TryComp<TechnologyDatabaseComponent>(serverUid, out var database))
        {
            serverName = server.ServerName;
            balances = new(server.PointBalances);
            experiments = _scanner.GetUiEntries(database, ExperimentSource.MachineScanner);
        }

        _ui.SetUiState(ent.Owner, ResearchExperimentMachineUiKey.Key, new ResearchExperimentMachineBuiState(
            serverName,
            balances,
            experiments,
            ent.Comp.Processing ? Loc.GetString("research-experiment-machine-status-processing") : Loc.GetString("research-experiment-machine-status-idle"),
            ent.Comp.LastResult));
    }
}
