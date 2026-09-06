// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Linq;
using Content.Server.Research.Systems;
using Content.Shared._Onyx.Research.Components;
using Content.Shared._Onyx.Research.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Research.Components;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Research.Systems;

public sealed partial class ResearchExperimentScannerSystem : EntitySystem
{
    [Dependency] private ResearchSystem _research = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ResearchExperimentScannerComponent, AfterInteractEvent>(OnInteract);
        SubscribeLocalEvent<ResearchExperimentScannerComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<ResearchExperimentScannerComponent, OpenExperimentServerMenuMessage>(OnOpenServerMenu);
        SubscribeLocalEvent<ResearchExperimentScannerComponent, ResearchRegistrationChangedEvent>(OnRegistrationChanged);
    }

    private void OnInteract(Entity<ResearchExperimentScannerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target ||
            !_interaction.InRangeUnobstructed(args.User, target, ent.Comp.Range))
            return;

        args.Handled = true;
        if (!_research.TryGetClientServer(ent, out var server, out _))
        {
            Fail(ent, args.User, "research-experiment-scanner-no-server");
            return;
        }

        if (!_research.TryProgressExperiment(server.Value,
                target,
                args.User,
                ExperimentSource.HandheldScanner,
                out _,
                out _,
                out var result))
        {
            Fail(ent, args.User, result switch
            {
                ResearchExperimentAttemptResult.NoCompatibleExperiment => "research-experiment-scanner-no-compatible",
                ResearchExperimentAttemptResult.AlreadyScanned => "research-experiment-scanner-duplicate",
                _ => "research-experiment-scanner-no-match",
            });
            return;
        }

        ent.Comp.LastResult = Loc.GetString("research-experiment-scanner-success", ("target", Name(target)));
        _audio.PlayPvs(ent.Comp.SuccessSound, ent);
        _popup.PopupEntity(ent.Comp.LastResult, ent, args.User, PopupType.SmallCaution);
        UpdateUi(ent);
    }

    private void OnUiOpened(Entity<ResearchExperimentScannerComponent> ent, ref BoundUIOpenedEvent args) => UpdateUi(ent);
    private void OnRegistrationChanged(Entity<ResearchExperimentScannerComponent> ent, ref ResearchRegistrationChangedEvent args) => UpdateUi(ent);
    private void OnOpenServerMenu(Entity<ResearchExperimentScannerComponent> ent, ref OpenExperimentServerMenuMessage args) =>
        _ui.TryToggleUi(ent.Owner, ResearchClientUiKey.Key, args.Actor);

    private void Fail(Entity<ResearchExperimentScannerComponent> ent, EntityUid user, string message)
    {
        ent.Comp.LastResult = Loc.GetString(message);
        _audio.PlayPvs(ent.Comp.FailureSound, ent);
        _popup.PopupEntity(ent.Comp.LastResult, ent, user);
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<ResearchExperimentScannerComponent> ent)
    {
        string? serverName = null;
        var experiments = new List<ResearchExperimentUiEntry>();
        if (_research.TryGetClientServer(ent, out var serverUid, out var server) &&
            TryComp<TechnologyDatabaseComponent>(serverUid, out var database))
        {
            serverName = server.ServerName;
            experiments = GetUiEntries(database, ExperimentSource.HandheldScanner);
        }

        _ui.SetUiState(ent.Owner, ResearchExperimentScannerUiKey.Key,
            new ResearchExperimentScannerState(serverName, experiments, ent.Comp.LastResult));
    }

    public List<ResearchExperimentUiEntry> GetUiEntries(TechnologyDatabaseComponent database, ExperimentSource source)
    {
        var entries = new List<ResearchExperimentUiEntry>();
        var active = new HashSet<ProtoId<ResearchExperimentPrototype>>();
        foreach (var id in database.ActiveExperiments)
            active.Add(id);
        var completed = new HashSet<ProtoId<ResearchExperimentPrototype>>();
        foreach (var id in database.CompletedExperiments)
            completed.Add(id);

        foreach (var experiment in _prototype.EnumeratePrototypes<ResearchExperimentPrototype>()
                     .Where(experiment => !experiment.Hidden && (experiment.SupportedSources & source) != 0)
                     .OrderBy(experiment => completed.Contains(experiment.ID))
                     .ThenByDescending(experiment => active.Contains(experiment.ID))
                     .ThenBy(experiment => Loc.GetString(experiment.Name)))
        {
            var progressIndex = -1;
            for (var i = 0; i < database.ExperimentProgress.Count; i++)
            {
                if (database.ExperimentProgress[i].Experiment != experiment.ID)
                    continue;

                progressIndex = i;
                break;
            }
            var tasks = new List<ResearchExperimentTaskUiEntry>();
            for (var i = 0; i < experiment.Tasks.Count; i++)
            {
                var taskProgress = progressIndex >= 0 && i < database.ExperimentProgress[progressIndex].Tasks.Count
                    ? database.ExperimentProgress[progressIndex].Tasks[i]
                    : default;
                tasks.Add(new ResearchExperimentTaskUiEntry(
                    Loc.GetString(experiment.Tasks[i].Goal),
                    taskProgress.Progress,
                    taskProgress.Target > 0 ? taskProgress.Target : Math.Max(1, experiment.Tasks[i].Target)));
            }

            entries.Add(new ResearchExperimentUiEntry(
                Loc.GetString(experiment.Name),
                Loc.GetString(experiment.Description),
                tasks,
                completed.Contains(experiment.ID)
                    ? ResearchExperimentUiStatus.Completed
                    : active.Contains(experiment.ID)
                        ? ResearchExperimentUiStatus.Active
                        : ResearchExperimentUiStatus.Locked));
        }
        return entries;
    }
}
