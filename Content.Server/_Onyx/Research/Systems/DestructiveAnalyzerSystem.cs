using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Research.Systems;
using Content.Shared._Onyx.Research;
using Content.Shared._Onyx.Research.Components;
using Content.Shared.Chat;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Research.Systems;
using Content.Shared.Stacks;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Research.Systems;

public sealed partial class DestructiveAnalyzerSystem : EntitySystem
{
    [Dependency] private ResearchSystem _research = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DestructiveAnalyzerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DestructiveAnalyzerComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
        SubscribeLocalEvent<DestructiveAnalyzerComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<DestructiveAnalyzerComponent, OpenResearchServerMenuMessage>(OnOpenServerMenu);
        SubscribeLocalEvent<DestructiveAnalyzerComponent, DestructiveAnalyzerSelectMethodMessage>(OnSelectMethod);
        SubscribeLocalEvent<DestructiveAnalyzerComponent, DestructiveAnalyzerRunMessage>(OnRun);
        SubscribeLocalEvent<DestructiveAnalyzerComponent, DestructiveAnalyzerEjectMessage>(OnEject);
        SubscribeLocalEvent<DestructiveAnalyzerComponent, ResearchServerPointsChangedEvent>(OnPointsChanged);
        SubscribeLocalEvent<DestructiveAnalyzerComponent, ResearchServerPointTypeChangedEvent>(OnPointsChanged);
        SubscribeLocalEvent<DestructiveAnalyzerComponent, ResearchRegistrationChangedEvent>(OnRegistrationChanged);
    }

    private void OnStartup(Entity<DestructiveAnalyzerComponent> ent, ref ComponentStartup args)
    {
        _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);
        UpdateAppearance(ent, DestructiveAnalyzerVisualState.Idle);
    }

    private void OnUiOpened(Entity<DestructiveAnalyzerComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnOpenServerMenu(Entity<DestructiveAnalyzerComponent> ent, ref OpenResearchServerMenuMessage args)
    {
        _ui.TryToggleUi(ent.Owner, ResearchClientUiKey.Key, args.Actor);
    }

    private void OnPointsChanged(Entity<DestructiveAnalyzerComponent> ent, ref ResearchServerPointsChangedEvent args)
    {
        UpdateUiIfOpen(ent);
    }

    private void OnPointsChanged(Entity<DestructiveAnalyzerComponent> ent, ref ResearchServerPointTypeChangedEvent args)
    {
        UpdateUiIfOpen(ent);
    }

    private void OnRegistrationChanged(Entity<DestructiveAnalyzerComponent> ent, ref ResearchRegistrationChangedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnAfterInteractUsing(Entity<DestructiveAnalyzerComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (ent.Comp.InsertedItem != null || ent.Comp.IsProcessing)
            return;

        var used = args.Used;
        var container = _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);
        if (!_container.Insert(used, container))
            return;

        ent.Comp.InsertedItem = used;
        ent.Comp.LastItemAnalyzed = false;
        ent.Comp.LastSubject = Name(used);
        ent.Comp.SelectedMethod = null;
        ent.Comp.LastResult = Loc.GetString("research-machine-destructive-item-loaded");

        UpdateAppearance(ent, DestructiveAnalyzerVisualState.Inserting);
        Timer.Spawn(ent.Comp.InsertAnimationDuration,
            () =>
            {
                if (TerminatingOrDeleted(ent) || ent.Comp.InsertedItem != used)
                    return;

                UpdateAppearance(ent, DestructiveAnalyzerVisualState.Loaded);
            });

        UpdateUi(ent);
        args.Handled = true;
    }

    private void OnSelectMethod(Entity<DestructiveAnalyzerComponent> ent, ref DestructiveAnalyzerSelectMethodMessage args)
    {
        ent.Comp.SelectedMethod = args.MethodId;
        UpdateUi(ent);
    }

    private void OnRun(Entity<DestructiveAnalyzerComponent> ent, ref DestructiveAnalyzerRunMessage args)
    {
        if (ent.Comp.IsProcessing)
        {
            Fail(ent, "research-machine-destructive-busy");
            return;
        }

        if (ent.Comp.LastItemAnalyzed)
        {
            Fail(ent, "research-machine-destructive-already-analyzed");
            return;
        }

        if (ent.Comp.InsertedItem is not { } used)
        {
            Fail(ent, "research-machine-destructive-no-item");
            return;
        }

        if (!TryResolveServer(ent, out var server))
        {
            Fail(ent, "research-machine-common-no-server");
            return;
        }

        if (!_container.TryGetContainingContainer(used, out var containing) || containing.Owner != ent.Owner)
        {
            ClearInsertedItem(ent);
            UpdateAppearance(ent, DestructiveAnalyzerVisualState.Idle);
            Fail(ent, "research-machine-destructive-no-item");
            return;
        }

        if (TryComp<MobStateComponent>(used, out var mobState) && mobState.CurrentState == MobState.Alive)
        {
            Fail(ent, "research-machine-destructive-living-subject-blocked");
            return;
        }

        if (!TryValidateAnalysis(ent, used, server, out var method))
            return;

        ent.Comp.IsProcessing = true;
        ent.Comp.LastResult = Loc.GetString("research-machine-destructive-processing");
        UpdateAppearance(ent, DestructiveAnalyzerVisualState.Deconstructing);
        UpdateUi(ent);

        // Rewards are applied only when the process finishes and the sample is destroyed.
        var actor = args.Actor;
        Timer.Spawn(ent.Comp.DeconstructAnimationDuration,
            () => CompleteAnalysis(ent, used, server, method, actor));
    }

    private bool TryValidateAnalysis(
        Entity<DestructiveAnalyzerComponent> ent,
        EntityUid sample,
        EntityUid server,
        [NotNullWhen(true)] out string? method)
    {
        method = null;

        var methods = GetAvailableMethods(sample, server);
        var selected = ent.Comp.SelectedMethod;
        if (string.IsNullOrWhiteSpace(selected) || !methods.Contains(selected))
        {
            selected = methods.FirstOrDefault();
            ent.Comp.SelectedMethod = selected;
        }

        if (string.IsNullOrWhiteSpace(selected))
        {
            Fail(ent, TryComp<ResearchAnalyzableComponent>(sample, out _)
                ? "research-machine-destructive-unsupported-method"
                : "research-machine-destructive-invalid-item");
            return false;
        }

        if (!TryGetRevealTechnologyFromMethod(selected, out _) &&
            !TryGetItemRequirementFromMethod(selected, out _, out _, out _) &&
            !TryComp<ResearchAnalyzableComponent>(sample, out var analyzable))
        {
            Fail(ent, "research-machine-destructive-invalid-item");
            return false;
        }

        method = selected;
        return true;
    }

    private void CompleteAnalysis(
        Entity<DestructiveAnalyzerComponent> ent,
        EntityUid used,
        EntityUid server,
        string method,
        EntityUid actor)
    {
        if (TerminatingOrDeleted(ent))
            return;

        ent.Comp.IsProcessing = false;

        if (TerminatingOrDeleted(used))
        {
            ClearInsertedItem(ent);
            UpdateAppearance(ent, DestructiveAnalyzerVisualState.Idle);
            UpdateUi(ent);
            return;
        }

        string rewardSummary;
        if (TryGetItemRequirementFromMethod(method, out var requiredTechnology, out var requirement, out var reveals))
        {
            if (!_research.CompleteItemRequirement(
                    server,
                    requiredTechnology,
                    requirement,
                    reveals,
                    out var revealed,
                    out var progress,
                    out var amount))
            {
                Fail(ent, "research-machine-destructive-invalid-item");
                UpdateAppearance(ent, DestructiveAnalyzerVisualState.Idle);
                return;
            }

            rewardSummary = revealed
                ? Loc.GetString("research-machine-destructive-result-revealed-tech",
                    ("technology", GetTechnologyName(requiredTechnology)))
                : Loc.GetString("research-machine-destructive-result-requirement-progress",
                    ("technology", GetTechnologyName(requiredTechnology)),
                    ("progress", progress),
                    ("amount", amount),
                    ("remaining", amount - progress));
        }
        else if (TryGetRevealTechnologyFromMethod(method, out var revealTechnology))
        {
            if (!_research.RevealTechnology(server, revealTechnology))
            {
                Fail(ent, "research-machine-destructive-invalid-item");
                UpdateAppearance(ent, DestructiveAnalyzerVisualState.Idle);
                return;
            }

            rewardSummary = Loc.GetString("research-machine-destructive-result-revealed-tech",
                ("technology", GetTechnologyName(revealTechnology)));

            _research.LogNetworkEvent(server,
                ResearchNetworkLogType.TechnologyRevealed,
                Loc.GetString("research-network-log-destructive-revealed",
                    ("user", _research.GetResearchLogUserName(actor)),
                    ("subject", Name(used)),
                    ("technology", GetTechnologyName(revealTechnology))));
        }
        else
        {
            if (!TryComp<ResearchAnalyzableComponent>(used, out var analyzable) ||
                !analyzable.MethodPointRewards.TryGetValue(method, out var rewards))
            {
                Fail(ent, "research-machine-destructive-unsupported-method");
                UpdateAppearance(ent, DestructiveAnalyzerVisualState.Idle);
                return;
            }

            var stackMultiplier = 1;
            if (TryComp<StackComponent>(used, out var stack))
                stackMultiplier = stack.Count;

            foreach (var reward in SharedResearchSystem.AggregatePoints(rewards))
            {
                _research.ModifyServerPoints(server, reward.Type, reward.Amount * stackMultiplier);
            }

            foreach (var technology in analyzable.UnlockTechnologies)
            {
                _research.AddTechnology(server, technology);
            }

            foreach (var technology in analyzable.RevealTechnologies)
            {
                _research.RevealTechnology(server, technology);
            }

            rewardSummary = BuildRewardSummary(rewards, stackMultiplier, analyzable);

            _research.LogNetworkEvent(server,
                ResearchNetworkLogType.PointsChanged,
                Loc.GetString("research-network-log-destructive-analyzed",
                    ("user", _research.GetResearchLogUserName(actor)),
                    ("subject", Name(used)),
                    ("method", LocalizeMethod(method)),
                    ("result", rewardSummary)));
        }

        if (TerminatingOrDeleted(used))
        {
            ClearInsertedItem(ent);
            UpdateAppearance(ent, DestructiveAnalyzerVisualState.Idle);
            UpdateUi(ent);
            return;
        }

        ent.Comp.LastItemAnalyzed = true;
        ent.Comp.LastResult = Loc.GetString("research-machine-destructive-last-result-success", ("result", rewardSummary));
        if (TryGetItemRequirementFromMethod(method, out _, out _, out _) &&
            TryComp<StackComponent>(used, out var remainingStack) &&
            remainingStack.Count > 1)
        {
            _stack.SetCount((used, remainingStack), remainingStack.Count - 1);
            var container = _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);
            _container.Remove(used, container);
            _transform.SetCoordinates(used, Transform(ent).Coordinates);
        }
        else
        {
            Del(used);
        }
        ClearInsertedItem(ent);
        UpdateAppearance(ent, DestructiveAnalyzerVisualState.Idle);
        _audio.PlayPvs(ent.Comp.SuccessSound, ent, ent.Comp.AudioParams);
        UpdateUi(ent);

        _popup.PopupEntity(Loc.GetString("research-destructive-analyzer-success"), ent, PopupType.SmallCaution);
        _chat.TrySendInGameICMessage(ent.Owner,
            Loc.GetString("research-machine-destructive-chat-result", ("result", rewardSummary)),
            InGameICChatType.Speak,
            false);
    }

    private void OnEject(Entity<DestructiveAnalyzerComponent> ent, ref DestructiveAnalyzerEjectMessage args)
    {
        if (ent.Comp.IsProcessing || ent.Comp.InsertedItem is not { } item)
            return;

        var container = _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);
        if (!_container.Remove(item, container))
            return;

        if (!_hands.TryPickupAnyHand(args.Actor, item))
            _transform.SetCoordinates(item, Transform(ent).Coordinates);

        ClearInsertedItem(ent);
        ent.Comp.LastResult = string.Empty;
        UpdateAppearance(ent, DestructiveAnalyzerVisualState.Idle);
        UpdateUi(ent);
    }

    private void Fail(Entity<DestructiveAnalyzerComponent> ent, string localeId)
    {
        ent.Comp.LastResult = Loc.GetString(localeId);
        _audio.PlayPvs(ent.Comp.FailureSound, ent, ent.Comp.AudioParams);
        UpdateUi(ent);
    }

    private void ClearInsertedItem(Entity<DestructiveAnalyzerComponent> ent)
    {
        ent.Comp.InsertedItem = null;
        ent.Comp.SelectedMethod = null;
    }

    private bool TryResolveServer(Entity<DestructiveAnalyzerComponent> ent, out EntityUid server)
    {
        server = default;

        if (TryComp<ResearchClientComponent>(ent, out var client) &&
            _research.TryGetClientServer(ent, out var registered, out _, client))
        {
            server = registered.Value;
            return true;
        }

        foreach (var candidate in _research.GetServers(ent).OrderBy(other => other.Comp.Id))
        {
            if (_research.TryGetNetworkAuthority(candidate.Owner, out var authority, out _, candidate.Comp))
            {
                server = authority;
                return true;
            }
        }

        return false;
    }

    private void UpdateAppearance(Entity<DestructiveAnalyzerComponent> ent, DestructiveAnalyzerVisualState state)
    {
        _appearance.SetData(ent.Owner, DestructiveAnalyzerVisuals.State, state);
    }

    private static bool TryGetRevealTechnologyFromMethod(
        string methodId,
        [NotNullWhen(true)] out string? technologyId)
    {
        const string prefix = "reveal:";
        if (!methodId.StartsWith(prefix, StringComparison.Ordinal))
        {
            technologyId = null;
            return false;
        }

        technologyId = methodId[prefix.Length..];
        return !string.IsNullOrWhiteSpace(technologyId);
    }

    private static bool TryGetItemRequirementFromMethod(
        string methodId,
        [NotNullWhen(true)] out string? technologyId,
        out int requirement,
        out bool reveals)
    {
        const string revealPrefix = "requirement:reveal:";
        const string researchPrefix = "requirement:research:";
        technologyId = null;
        requirement = -1;
        reveals = methodId.StartsWith(revealPrefix, StringComparison.Ordinal);
        var prefix = reveals ? revealPrefix : researchPrefix;
        if (!methodId.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        var separator = methodId.LastIndexOf(':');
        if (separator <= prefix.Length ||
            !int.TryParse(methodId[(separator + 1)..], out requirement))
            return false;

        technologyId = methodId[prefix.Length..separator];
        return !string.IsNullOrWhiteSpace(technologyId);
    }

    private List<string> GetAvailableMethods(
        EntityUid sample,
        EntityUid server,
        Dictionary<string, DestructiveAnalyzerRequirementState>? requirementStates = null)
    {
        var methods = new List<string>();
        foreach (var match in _research.GetTechnologyRequirementsForItem(server, sample))
        {
            var id = $"requirement:{(match.Reveals ? "reveal" : "research")}:{match.Technology}:{match.Requirement}";
            methods.Add(id);
            requirementStates?[id] = new DestructiveAnalyzerRequirementState(
                match.Technology,
                match.Reveals,
                match.Progress,
                match.Amount);
        }

        if (TryComp<ResearchAnalyzableComponent>(sample, out var analyzable))
        {
            methods.AddRange(analyzable.SupportedMethods.Count > 0
                ? analyzable.SupportedMethods.Where(analyzable.MethodPointRewards.ContainsKey)
                : analyzable.MethodPointRewards.Keys);

            // Образцы без методов с очками (только раскрытие) получают reveal-методы.
            if (methods.Count == 0 && analyzable.RevealTechnologies.Count > 0)
            {
                foreach (var technology in analyzable.RevealTechnologies)
                {
                    var id = $"reveal:{technology}";
                    methods.Add(id);
                    requirementStates?[id] = new DestructiveAnalyzerRequirementState(technology, true, 0, 1);
                }
            }
        }

        return methods;
    }

    private string BuildRewardSummary(List<ResearchPointAmount> rewards, int stackMultiplier, ResearchAnalyzableComponent analyzable)
    {
        var segments = new List<string>();

        var totals = SharedResearchSystem.AggregatePoints(rewards.Select(reward =>
            new ResearchPointAmount(reward.Type, reward.Amount * stackMultiplier)));
        if (totals.Count > 0)
        {
            var pointsText = totals
                .OrderBy(pair => (string) pair.Type)
                .Select(pair => Loc.GetString("research-machine-destructive-result-points-entry",
                    ("type", _research.GetPointTypeName(pair.Type)),
                    ("amount", pair.Amount)));
            segments.Add(Loc.GetString("research-machine-destructive-result-points",
                ("points", string.Join(", ", pointsText))));
        }

        if (analyzable.RevealTechnologies.Count > 0)
        {
            var revealed = analyzable.RevealTechnologies.Select(GetTechnologyName);
            segments.Add(Loc.GetString("research-machine-destructive-result-revealed-tech",
                ("technology", string.Join(", ", revealed))));
        }

        if (analyzable.UnlockTechnologies.Count > 0)
        {
            var technologies = analyzable.UnlockTechnologies.Select(GetTechnologyName);
            segments.Add(Loc.GetString("research-machine-destructive-result-unlocked-tech",
                ("technology", string.Join(", ", technologies))));
        }

        return segments.Count > 0
            ? string.Join(", ", segments)
            : Loc.GetString("research-machine-destructive-result-generic");
    }

    private string GetTechnologyName(ProtoId<TechnologyPrototype> technologyId)
    {
        return _prototype.TryIndex<TechnologyPrototype>(technologyId, out var prototype)
            ? Loc.GetString(prototype.Name)
            : technologyId;
    }

    private string LocalizeMethod(string methodId)
    {
        if (TryGetItemRequirementFromMethod(methodId, out var requiredTechnology, out _, out var reveals))
            return Loc.GetString(reveals
                    ? "research-machine-destructive-method-complete-reveal-requirement"
                    : "research-machine-destructive-method-complete-research-requirement",
                ("technology", GetTechnologyName(requiredTechnology)));

        if (TryGetRevealTechnologyFromMethod(methodId, out var revealTechnology))
            return Loc.GetString("research-machine-destructive-method-reveal-technology",
                ("technology", GetTechnologyName(revealTechnology)));

        return Loc.TryGetString($"research-machine-destructive-method-{methodId.ToLowerInvariant()}", out var localized)
            ? localized
            : Loc.GetString("research-machine-destructive-method-unknown");
    }

    private void UpdateUiIfOpen(Entity<DestructiveAnalyzerComponent> ent)
    {
        if (_ui.IsUiOpen(ent.Owner, DestructiveAnalyzerUiKey.Key))
            UpdateUi(ent);
    }

    private void UpdateUi(Entity<DestructiveAnalyzerComponent> ent)
    {
        string? serverName = null;
        var pointBalances = new List<ResearchPointAmount>();
        var methods = new List<string>();
        var requirementStates = new Dictionary<string, DestructiveAnalyzerRequirementState>();
        EntityUid? server = null;

        if (TryComp<ResearchClientComponent>(ent, out var client) &&
            _research.TryGetClientServer(ent, out server, out var serverComponent, client))
        {
            serverName = serverComponent.ServerName;
            pointBalances = serverComponent.PointBalances.ToList();
        }

        if (ent.Comp.InsertedItem is { } used && server != null)
        {
            methods = GetAvailableMethods(used, server.Value, requirementStates);
            if (ent.Comp.SelectedMethod == null || !methods.Contains(ent.Comp.SelectedMethod))
                ent.Comp.SelectedMethod = methods.FirstOrDefault();
        }

        var state = new DestructiveAnalyzerBoundInterfaceState(
            serverName,
            pointBalances,
            ent.Comp.LastSubject,
            ent.Comp.LastResult,
            ent.Comp.InsertedItem is { } item ? Name(item) : null,
            ent.Comp.InsertedItem is { } inserted ? GetNetEntity(inserted) : null,
            ent.Comp.SelectedMethod,
            methods,
            requirementStates);

        _ui.SetUiState(ent.Owner, DestructiveAnalyzerUiKey.Key, state);
    }
}
