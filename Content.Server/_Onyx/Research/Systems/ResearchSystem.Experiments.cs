// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Linq;
using Content.Server.Research.Components;
using Content.Shared._Onyx.Fishing.Components;
using Content.Shared._Onyx.Research;
using Content.Shared._Onyx.Research.Prototypes;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Explosion.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server.Research.Systems;

public sealed partial class ResearchSystem
{
    [Dependency] private TagSystem _experimentTags = default!;
    [Dependency] private SharedSolutionContainerSystem _experimentSolutions = default!;
    [Dependency] private DamageableSystem _experimentDamageable = default!;

    private void InitializeExperiments()
    {
        SubscribeLocalEvent<TechnologyDatabaseComponent, ComponentStartup>(OnExperimentDatabaseStartup);
        SubscribeLocalEvent<ResearchConsoleComponent, AfterInteractUsingEvent>(OnExperimentConsoleInteract);
    }

    private void OnExperimentDatabaseStartup(Entity<TechnologyDatabaseComponent> ent, ref ComponentStartup args)
    {
        RefreshExperiments(ent.Comp);
        Dirty(ent);
    }

    private void OnExperimentConsoleInteract(Entity<ResearchConsoleComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (args.Handled || !TryGetClientServer(ent, out var server, out _))
            return;

        if (!TryProgressExperiment(server.Value,
                args.Used,
                args.User,
                ExperimentSource.ResearchConsole,
                out _,
                out _,
                out _))
            return;

        args.Handled = true;
        SyncClientWithServer(ent);
    }

    public void RefreshExperiments(TechnologyDatabaseComponent database)
    {
        var available = new HashSet<ProtoId<ResearchExperimentPrototype>>();
        foreach (var experiment in ProtoMan.EnumeratePrototypes<ResearchExperimentPrototype>())
        {
            if (database.CompletedExperiments.Contains(experiment.ID))
                continue;

            var explicitlyUnlocked = experiment.StartingExperiment || database.UnlockedExperiments.Contains(experiment.ID);
            var hasPrerequisites = experiment.RequiredTechnologies.Count > 0 || experiment.RequiredExperiments.Count > 0;
            var prerequisitesMet = hasPrerequisites &&
                                   experiment.RequiredTechnologies.All(database.UnlockedTechnologies.Contains) &&
                                   experiment.RequiredExperiments.All(database.CompletedExperiments.Contains);
            if (explicitlyUnlocked || prerequisitesMet)
                available.Add(experiment.ID);
        }

        foreach (var technologyId in database.UnlockedTechnologies)
        {
            if (!ProtoMan.TryIndex<TechnologyPrototype>(technologyId, out var technology))
                continue;
            available.UnionWith(technology.UnlockedExperiments);
        }

        available.ExceptWith(database.CompletedExperiments);

        database.ActiveExperiments = available.ToList();
        database.ExperimentProgress.RemoveAll(progress =>
            !available.Contains(progress.Experiment) && !database.CompletedExperiments.Contains(progress.Experiment));

        foreach (var experimentId in available)
        {
            if (!ProtoMan.TryIndex(experimentId, out var experiment))
                continue;

            var progressIndex = database.ExperimentProgress.FindIndex(progress => progress.Experiment == experimentId);
            var progress = progressIndex >= 0
                ? database.ExperimentProgress[progressIndex]
                : new ResearchExperimentProgress { Experiment = experimentId };
            while (progress.Tasks.Count < experiment.Tasks.Count)
                progress.Tasks.Add(new ResearchExperimentTaskProgress());
            if (progress.Tasks.Count > experiment.Tasks.Count)
                progress.Tasks.RemoveRange(experiment.Tasks.Count, progress.Tasks.Count - experiment.Tasks.Count);
            for (var taskIndex = 0; taskIndex < experiment.Tasks.Count; taskIndex++)
            {
                var taskProgress = progress.Tasks[taskIndex];
                taskProgress.Target = Math.Max(1, experiment.Tasks[taskIndex].Target);
                taskProgress.Progress = Math.Min(taskProgress.Progress, taskProgress.Target);
                progress.Tasks[taskIndex] = taskProgress;
            }

            if (progressIndex >= 0)
                database.ExperimentProgress[progressIndex] = progress;
            else
                database.ExperimentProgress.Add(progress);
        }
    }

    public bool TryProgressExperiment(
        EntityUid serverUid,
        EntityUid subject,
        EntityUid? user,
        ExperimentSource source,
        out bool changed,
        out List<ProtoId<ResearchExperimentPrototype>> completed,
        out ResearchExperimentAttemptResult result)
    {
        changed = false;
        completed = [];
        result = ResearchExperimentAttemptResult.NoMatch;

        if (!TryGetNetworkAuthority(serverUid, out var authority, out var server) ||
            !TryComp<TechnologyDatabaseComponent>(authority, out var database))
            return false;

        var compatible = false;
        var duplicate = false;
        foreach (var experimentId in database.ActiveExperiments.ToArray())
        {
            if (!ProtoMan.TryIndex(experimentId, out var experiment) || (experiment.SupportedSources & source) == 0)
                continue;

            compatible = true;
            var index = database.ExperimentProgress.FindIndex(progress => progress.Experiment == experimentId);
            if (index < 0)
                continue;

            var progress = database.ExperimentProgress[index];
            var netSubject = GetNetEntity(subject);
            var matched = false;
            for (var taskIndex = 0; taskIndex < experiment.Tasks.Count && taskIndex < progress.Tasks.Count; taskIndex++)
            {
                var task = experiment.Tasks[taskIndex];
                var taskProgress = progress.Tasks[taskIndex];
                if (taskProgress.Progress >= taskProgress.Target ||
                    !task.AnyOf.Any(requirement => MatchesExperiment(subject, requirement)))
                    continue;

                if ((!task.AllowRepeatedEntities && taskProgress.ScannedEntities.Contains(netSubject)) ||
                    (task.RequireDifferentPrototypes && !IsNewPrototype(subject, taskProgress)))
                {
                    duplicate = true;
                    continue;
                }

                if (!task.AllowRepeatedEntities)
                    taskProgress.ScannedEntities.Add(netSubject);
                if (task.RequireDifferentPrototypes)
                    taskProgress.ScannedPrototypes.Add(MetaData(subject).EntityPrototype!.ID);
                taskProgress.Progress = Math.Min(taskProgress.Target, taskProgress.Progress + 1);
                progress.Tasks[taskIndex] = taskProgress;
                matched = true;
                break;
            }

            if (!matched)
                continue;

            database.ExperimentProgress[index] = progress;
            changed = true;

            if (progress.Tasks.Any(task => task.Progress < task.Target))
                continue;

            CompleteExperiment(authority, server, database, experiment, user);
            completed.Add(experiment.ID);
        }

        if (!changed)
        {
            result = !compatible
                ? ResearchExperimentAttemptResult.NoCompatibleExperiment
                : duplicate
                    ? ResearchExperimentAttemptResult.AlreadyScanned
                    : ResearchExperimentAttemptResult.NoMatch;
            return false;
        }

        result = ResearchExperimentAttemptResult.Progressed;
        RefreshExperiments(database);
        Dirty(authority, database);
        SynchronizeNetwork(authority, server);
        return true;
    }

    private void CompleteExperiment(
        EntityUid authority,
        ResearchServerComponent server,
        TechnologyDatabaseComponent database,
        ResearchExperimentPrototype experiment,
        EntityUid? user)
    {
        database.ActiveExperiments.Remove(experiment.ID);
        if (!database.CompletedExperiments.Contains(experiment.ID))
            database.CompletedExperiments.Add(experiment.ID);

        foreach (var reward in experiment.Reward.Points)
            ModifyServerPoints(authority, reward.Type, reward.Amount, server);
        foreach (var unlocked in experiment.Reward.UnlockExperiments)
        {
            if (!database.UnlockedExperiments.Contains(unlocked))
                database.UnlockedExperiments.Add(unlocked);
        }
        foreach (var technology in experiment.Reward.RevealTechnologies)
            RevealTechnology(authority, technology);

        LogNetworkEvent(authority,
            ResearchNetworkLogType.ExperimentCompleted,
            Loc.GetString("research-experiment-network-completed",
                ("experiment", Loc.GetString(experiment.Name)),
                ("user", GetResearchLogUserName(user))),
            server);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):player} completed research experiment {experiment.ID} on {ToPrettyString(authority)}.");
    }

    private bool MatchesExperiment(EntityUid subject, ResearchExperimentRequirement requirement)
    {
        var metadata = MetaData(subject);
        if (requirement.Prototypes.Count > 0 &&
            (metadata.EntityPrototype == null || !requirement.Prototypes.Contains(metadata.EntityPrototype.ID)))
            return false;

        foreach (var tag in requirement.Tags)
        {
            if (!_experimentTags.HasTag(subject, tag))
                return false;
        }

        foreach (var componentName in requirement.Components)
        {
            if (!EntityManager.ComponentFactory.TryGetRegistration(componentName, out var registration) ||
                !HasComp(subject, registration.Type))
                return false;
        }

        return MatchesConditions(subject, requirement) &&
               MatchesReagent(subject, requirement) &&
               MatchesGas(subject, requirement) &&
               (requirement.MinimumExplosiveIntensity == null ||
                TryComp<ExplosiveComponent>(subject, out var explosive) &&
                explosive.TotalIntensity >= requirement.MinimumExplosiveIntensity);
    }

    private bool IsNewPrototype(EntityUid subject, ResearchExperimentTaskProgress progress)
    {
        var prototype = MetaData(subject).EntityPrototype?.ID;
        return prototype != null && !progress.ScannedPrototypes.Contains(prototype);
    }

    private bool MatchesConditions(EntityUid subject, ResearchExperimentRequirement requirement)
    {
        foreach (var condition in requirement.Conditions)
        {
            var matches = condition switch
            {
                ResearchExperimentCondition.Fish => HasComp<FishComponent>(subject),
                ResearchExperimentCondition.RareFish => TryComp<FishComponent>(subject, out var fish) && fish.FishDifficulty >= 0.035f,
                ResearchExperimentCondition.Cyborg => HasComp<BorgChassisComponent>(subject),
                ResearchExperimentCondition.NonHumanHumanoid => TryComp<HumanoidProfileComponent>(subject, out var humanoid) && humanoid.Species != "Human",
                ResearchExperimentCondition.Damaged => TryComp<DamageableComponent>(subject, out var damage) && _experimentDamageable.GetTotalDamage((subject, damage)) > FixedPoint2.Zero,
                _ => false,
            };
            if (!matches)
                return false;
        }
        return true;
    }

    private bool MatchesReagent(EntityUid subject, ResearchExperimentRequirement requirement)
    {
        if (requirement.Reagent == null)
            return true;
        if (!TryComp<SolutionManagerComponent>(subject, out _))
            return false;

        var required = FixedPoint2.Zero;
        var total = FixedPoint2.Zero;
        foreach (var (_, solution) in _experimentSolutions.EnumerateSolutions(subject, includeSelf: true))
        {
            foreach (var reagent in solution.Comp.Solution.Contents)
            {
                total += reagent.Quantity;
                if (reagent.Reagent.Prototype == requirement.Reagent)
                    required += reagent.Quantity;
            }
        }

        return required > FixedPoint2.Zero &&
               (requirement.MinimumReagentPurity == null || total > FixedPoint2.Zero && (float) (required / total) >= requirement.MinimumReagentPurity);
    }

    private bool MatchesGas(EntityUid subject, ResearchExperimentRequirement requirement)
    {
        if (requirement.Gas == null)
            return true;
        if (!Enum.TryParse<Gas>(requirement.Gas, true, out var gas))
            return false;

        var mixture = TryComp<GasCanisterComponent>(subject, out var canister)
            ? canister.Air
            : TryComp<GasTankComponent>(subject, out var tank) ? tank.Air : null;
        if (mixture == null || mixture.TotalMoles <= 0f)
            return false;

        var required = mixture.GetMoles(gas);
        return required > 0f &&
               (requirement.MinimumGasPurity == null || required / mixture.TotalMoles >= requirement.MinimumGasPurity);
    }
}
