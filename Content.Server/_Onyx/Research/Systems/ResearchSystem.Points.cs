using Content.Shared._Onyx.Research;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;

namespace Content.Server.Research.Systems;

public sealed partial class ResearchSystem
{
    /// <summary>
    /// Adds (or removes) an amount of a specific point type to the network authority.
    /// </summary>
    public void ModifyServerPoints(EntityUid uid, string type, int points, ResearchServerComponent? component = null)
    {
        if (points == 0 || !Resolve(uid, ref component))
            return;

        if (!TryGetNetworkAuthority(uid, out var authority, out var authorityComponent, component))
            return;
        uid = authority;
        component = authorityComponent;

        EnsurePointBalance(component, type);
        var previousBalance = 0;
        var totalByType = 0;
        for (var i = 0; i < component.PointBalances.Count; i++)
        {
            if (component.PointBalances[i].Type != type)
                continue;

            previousBalance = component.PointBalances[i].Amount;
            totalByType = Math.Max(0, previousBalance + points);
            var balance = component.PointBalances[i];
            balance.Amount = totalByType;
            component.PointBalances[i] = balance;
            break;
        }

        if (type == ResearchPointAmount.General)
            component.Points = totalByType;

        var actualDelta = totalByType - previousBalance;
        var ev = new ResearchServerPointsChangedEvent(uid, component.Points, actualDelta);
        var typeEv = new ResearchServerPointTypeChangedEvent(uid, type, totalByType, actualDelta);
        foreach (var client in GetNetworkClients(uid, component))
        {
            RaiseLocalEvent(client, ref ev);
            RaiseLocalEvent(client, ref typeEv);
        }
        Dirty(uid, component);
        SynchronizeNetwork(uid, component);
    }

    public int GetPointBalance(EntityUid uid, string type, ResearchServerComponent? component = null)
    {
        return Resolve(uid, ref component, false)
            ? GetPointAmount(component.PointBalances, type)
            : 0;
    }

    private void EnsurePointBalance(ResearchServerComponent component, string type)
    {
        foreach (var balance in component.PointBalances)
        {
            if (balance.Type == type)
                return;
        }

        component.PointBalances.Add(new ResearchPointAmount(type, 0));
    }

    private bool HasSufficientPoints(Entity<ResearchServerComponent> server, IReadOnlyList<ResearchPointAmount> costs)
    {
        foreach (var cost in costs)
        {
            if (GetPointAmount(server.Comp.PointBalances, cost.Type) < cost.Amount)
                return false;
        }

        return true;
    }

    private void TryConsumePoints(EntityUid uid, IReadOnlyList<ResearchPointAmount> costs, ResearchServerComponent? component = null)
    {
        if (!Resolve(uid, ref component, false) ||
            !TryGetNetworkAuthority(uid, out var authority, out var authorityComponent, component) ||
            !HasSufficientPoints((authority, authorityComponent), costs))
            return;

        foreach (var cost in AggregatePoints(costs))
        {
            ModifyServerPoints(authority, cost.Type, -cost.Amount, authorityComponent);
        }
    }

    /// <summary>
    /// Checks whether a technology can be unlocked on the client's server,
    /// returning its effective typed costs.
    /// </summary>
    public bool CanServerUnlockTechnology(
        EntityUid uid,
        TechnologyPrototype technology,
        out List<ResearchPointAmount> finalCosts,
        TechnologyDatabaseComponent? database = null,
        ResearchClientComponent? client = null)
    {
        finalCosts = GetTechnologyCosts(technology);

        if (!Resolve(uid, ref client, ref database, false))
            return false;

        if (!TryGetClientServer(uid, out _, out var serverComp, client))
            return false;

        if (!IsTechnologyAvailable(database, technology))
            return false;

        return HasSufficientPoints((serverComp.Owner, serverComp), finalCosts);
    }
}
