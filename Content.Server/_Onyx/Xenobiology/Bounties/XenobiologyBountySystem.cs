using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Research.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Onyx.Xenobiology.Bounties;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Stacks;
using Content.Shared.Whitelist;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Xenobiology.Bounties;

public sealed partial class XenobiologyBountySystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private IdentitySystem _identity = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ResearchSystem _research = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedStackSystem _stacks = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private EntityQuery<ContainerManagerComponent> _containers;
    private EntityQuery<StackComponent> _stackQuery;

    public override void Initialize()
    {
        base.Initialize();
        _containers = GetEntityQuery<ContainerManagerComponent>();
        _stackQuery = GetEntityQuery<StackComponent>();

        SubscribeLocalEvent<StationXenobiologyBountyDatabaseComponent, MapInitEvent>(OnDatabaseMapInit);
        SubscribeLocalEvent<XenobiologyBountyConsoleComponent, BoundUIOpenedEvent>(OnConsoleOpened);
        SubscribeLocalEvent<XenobiologyBountyConsoleComponent, XenobiologyBountyFulfillMessage>(OnFulfill);
        SubscribeLocalEvent<XenobiologyBountyConsoleComponent, XenobiologyBountySkipMessage>(OnSkip);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<StationXenobiologyBountyDatabaseComponent>();
        while (query.MoveNext(out var station, out var database))
        {
            if (_timing.CurTime < database.NextRefreshTime)
                continue;

            database.Bounties.Clear();
            FillDatabase((station, database));
            database.NextRefreshTime = _timing.CurTime + database.RefreshDelay;
            Dirty(station, database);
            UpdateStationConsoles(station, database);
        }
    }

    private void OnDatabaseMapInit(Entity<StationXenobiologyBountyDatabaseComponent> station, ref MapInitEvent args)
    {
        FillDatabase(station);
        station.Comp.NextRefreshTime = _timing.CurTime + station.Comp.RefreshDelay;
        Dirty(station);
    }

    private void OnConsoleOpened(Entity<XenobiologyBountyConsoleComponent> console, ref BoundUIOpenedEvent args)
    {
        if (TryGetDatabase(console, out _, out var database))
            UpdateState(console, database);
    }

    private void OnFulfill(Entity<XenobiologyBountyConsoleComponent> console, ref XenobiologyBountyFulfillMessage args)
    {
        if (args.Actor is not { Valid: true } actor ||
            !HasAccess(console, actor) ||
            !TryGetDatabase(console, out var station, out var database) ||
            !TryGetBounty(database, args.BountyId, out var bounty) ||
            !_prototypes.TryIndex(bounty.Bounty, out var prototype) ||
            !_research.TryGetClientServer(console, out var researchServer, out var researchComponent) ||
            !TryBuildConsumptionPlan(actor, prototype.Entries, out var plan))
        {
            Deny(console);
            return;
        }

        if (plan.Any(item => TerminatingOrDeleted(item.Key) ||
                (_stackQuery.TryComp(item.Key, out var stack) && stack.Count < item.Value)))
        {
            Deny(console);
            return;
        }

        // Remove first so duplicate UI messages cannot award the same bounty twice.
        if (!RemoveBounty(database, bounty, false, actor))
            return;

        foreach (var (entity, amount) in plan)
        {
            if (_stackQuery.TryComp(entity, out var stack))
                _stacks.TryUse((entity, stack), amount);
            else
                QueueDel(entity);
        }

        _research.ModifyServerPoints(researchServer.Value, prototype.PointsAwarded, researchComponent);
        Dirty(station, database);
        _audio.PlayPvs(console.Comp.FulfillSound, console);
        UpdateStationConsoles(station, database);
    }

    private void OnSkip(Entity<XenobiologyBountyConsoleComponent> console, ref XenobiologyBountySkipMessage args)
    {
        if (args.Actor is not { Valid: true } actor ||
            !TryGetDatabase(console, out var station, out var database) ||
            _timing.CurTime < database.NextSkipTime ||
            !HasAccess(console, actor) ||
            !TryGetBounty(database, args.BountyId, out var bounty) ||
            !RemoveBounty(database, bounty, true, actor))
        {
            Deny(console);
            return;
        }

        database.NextSkipTime = _timing.CurTime + database.SkipDelay;
        FillDatabase((station, database));
        Dirty(station, database);
        UpdateStationConsoles(station, database);
    }

    public void FillDatabase(Entity<StationXenobiologyBountyDatabaseComponent> station)
    {
        var active = station.Comp.Bounties.Select(bounty => bounty.Bounty).ToHashSet();
        var pool = _prototypes.EnumeratePrototypes<XenobiologyBountyPrototype>()
            .Where(prototype => !active.Contains(prototype.ID))
            .ToList();
        _random.Shuffle(pool);

        while (station.Comp.Bounties.Count < station.Comp.MaxBounties && pool.Count > 0)
        {
            var prototype = pool[^1];
            pool.RemoveAt(pool.Count - 1);
            TryAddBounty(station, prototype);
        }

        SortBounties(station.Comp);
    }

    public bool TryAddBounty(Entity<StationXenobiologyBountyDatabaseComponent> station, XenobiologyBountyPrototype prototype)
    {
        if (station.Comp.Bounties.Count >= station.Comp.MaxBounties ||
            station.Comp.Bounties.Any(bounty => bounty.Bounty == prototype.ID))
            return false;

        for (var attempt = 0; attempt < 10_000; attempt++)
        {
            var bounty = new XenobiologyBountyData(prototype, station.Comp.NextIdentifier++);
            if (station.Comp.Bounties.Any(active => active.Id == bounty.Id) ||
                station.Comp.History.Any(history => history.Id == bounty.Id))
                continue;

            station.Comp.Bounties.Add(bounty);
            return true;
        }

        Log.Error("Unable to generate a unique xenobiology bounty ID for station {Station}", ToPrettyString(station));
        return false;
    }

    public bool TryBuildConsumptionPlan(
        EntityUid root,
        IReadOnlyList<XenobiologyBountyItemEntry> entries,
        out Dictionary<EntityUid, int> plan)
    {
        var entities = GetBountyEntities(root);
        var capacities = entities.ToDictionary(entity => entity, entity => _stackQuery.CompOrNull(entity)?.Count ?? 1);
        var ordered = entries
            .Select(entry => (Entry: entry, Candidates: entities.Where(entity => IsValid(entity, entry)).ToArray()))
            .OrderBy(entry => entry.Candidates.Length)
            .ToArray();
        plan = new Dictionary<EntityUid, int>();
        return Allocate(ordered, 0, 0, capacities, plan);
    }

    private bool Allocate(
        (XenobiologyBountyItemEntry Entry, EntityUid[] Candidates)[] entries,
        int entryIndex,
        int supplied,
        Dictionary<EntityUid, int> capacities,
        Dictionary<EntityUid, int> plan)
    {
        if (entryIndex == entries.Length)
            return true;

        var current = entries[entryIndex];
        if (supplied >= current.Entry.Amount)
            return Allocate(entries, entryIndex + 1, 0, capacities, plan);

        foreach (var entity in current.Candidates)
        {
            if (capacities[entity] <= 0)
                continue;

            capacities[entity]--;
            plan[entity] = plan.GetValueOrDefault(entity) + 1;
            if (Allocate(entries, entryIndex, supplied + 1, capacities, plan))
                return true;
            capacities[entity]++;
            if (--plan[entity] == 0)
                plan.Remove(entity);
        }

        return false;
    }

    private HashSet<EntityUid> GetBountyEntities(EntityUid root)
    {
        var visited = new HashSet<EntityUid>();
        var pending = new Stack<EntityUid>();
        pending.Push(root);
        while (pending.TryPop(out var entity))
        {
            if (!visited.Add(entity) || !_containers.TryComp(entity, out var manager))
                continue;

            foreach (var container in manager.Containers.Values)
            foreach (var child in container.ContainedEntities)
                pending.Push(child);
        }

        return visited;
    }

    private bool IsValid(EntityUid entity, XenobiologyBountyItemEntry entry)
    {
        return _whitelist.IsValid(entry.Whitelist, entity) &&
               (entry.Blacklist == null || !_whitelist.IsValid(entry.Blacklist, entity));
    }

    private bool RemoveBounty(
        StationXenobiologyBountyDatabaseComponent database,
        XenobiologyBountyData bounty,
        bool skipped,
        EntityUid actor)
    {
        if (!database.Bounties.Remove(bounty))
            return false;

        database.History.Add(new XenobiologyBountyHistoryData(
            bounty,
            skipped ? CargoBountyHistoryData.BountyResult.Skipped : CargoBountyHistoryData.BountyResult.Completed,
            _timing.CurTime,
            _identity.GetIdentityShortInfo(actor, actor)));
        return true;
    }

    private bool HasAccess(Entity<XenobiologyBountyConsoleComponent> console, EntityUid actor)
    {
        return !TryComp<AccessReaderComponent>(console, out var reader) || _access.IsAllowed(actor, console, reader);
    }

    private void Deny(Entity<XenobiologyBountyConsoleComponent> console)
    {
        if (_timing.CurTime < console.Comp.NextDenySoundTime)
            return;
        console.Comp.NextDenySoundTime = _timing.CurTime + console.Comp.DenySoundDelay;
        Dirty(console);
        _audio.PlayPvs(console.Comp.DenySound, console);
    }

    private bool TryGetDatabase(
        EntityUid console,
        out EntityUid station,
        [NotNullWhen(true)] out StationXenobiologyBountyDatabaseComponent? database)
    {
        database = null;
        station = _station.GetOwningStation(console) ?? EntityUid.Invalid;
        return station.Valid && TryComp(station, out database);
    }

    private static bool TryGetBounty(
        StationXenobiologyBountyDatabaseComponent database,
        string id,
        out XenobiologyBountyData bounty)
    {
        foreach (var active in database.Bounties)
        {
            if (active.Id != id)
                continue;
            bounty = active;
            return true;
        }

        bounty = default;
        return false;
    }

    private void UpdateStationConsoles(EntityUid station, StationXenobiologyBountyDatabaseComponent database)
    {
        SortBounties(database);
        var query = EntityQueryEnumerator<XenobiologyBountyConsoleComponent, UserInterfaceComponent>();
        while (query.MoveNext(out var console, out _, out _))
        {
            if (_station.GetOwningStation(console) == station)
                UpdateState(console, database);
        }
    }

    private void UpdateState(EntityUid console, StationXenobiologyBountyDatabaseComponent database)
    {
        SortBounties(database);
        _ui.SetUiState(console, CargoConsoleUiKey.Bounty, new XenobiologyBountyConsoleState(
            new List<XenobiologyBountyData>(database.Bounties),
            new List<XenobiologyBountyHistoryData>(database.History),
            database.NextSkipTime - _timing.CurTime,
            database.NextRefreshTime - _timing.CurTime));
    }

    private void SortBounties(StationXenobiologyBountyDatabaseComponent database)
    {
        database.Bounties.Sort((left, right) =>
        {
            var leftPoints = _prototypes.TryIndex(left.Bounty, out var leftPrototype) ? leftPrototype.PointsAwarded : 0;
            var rightPoints = _prototypes.TryIndex(right.Bounty, out var rightPrototype) ? rightPrototype.PointsAwarded : 0;
            var comparison = leftPoints.CompareTo(rightPoints);
            return comparison != 0 ? comparison : string.CompareOrdinal(left.Id, right.Id);
        });
    }
}
