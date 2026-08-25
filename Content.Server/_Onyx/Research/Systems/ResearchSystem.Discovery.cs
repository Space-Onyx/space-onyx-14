using Content.Shared._Onyx.Research;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Research.Systems;

public sealed partial class ResearchSystem
{
    /// <summary>
    /// Reveals a hidden technology on the client's network authority,
    /// making it visible and purchasable at the console.
    /// </summary>
    public bool RevealTechnology(
        EntityUid client,
        ProtoId<TechnologyPrototype> technology,
        ResearchClientComponent? clientComponent = null)
    {
        if (!TryGetClientServer(client, out var server, out _, clientComponent))
            return false;

        return RevealTechnology(server.Value, technology);
    }

    /// <summary>
    /// Reveals a hidden technology on the network authority of the given server.
    /// </summary>
    public bool RevealTechnology(
        EntityUid server,
        ProtoId<TechnologyPrototype> technology)
    {
        if (!TryGetNetworkAuthority(server, out var authority, out var authorityComponent) ||
            !TryComp<TechnologyDatabaseComponent>(authority, out var database))
            return false;

        if (!ProtoMan.TryIndex<TechnologyPrototype>(technology, out var prototype) ||
            database.UnlockedTechnologies.Contains(technology))
            return false;

        if (!database.RevealedTechnologies.Contains(technology))
            database.RevealedTechnologies.Add(technology);

        Dirty(authority, database);
        UpdateTechnologyCards(authority, database);
        SynchronizeNetwork(authority, authorityComponent);

        LogNetworkEvent(authority, ResearchNetworkLogType.TechnologyRevealed,
            Loc.GetString("research-network-log-technology-revealed",
                ("technology", Loc.GetString(prototype.Name))));

        var ev = new TechnologyDatabaseModifiedEvent(null);
        RaiseLocalEvent(authority, ref ev);
        return true;
    }

    /// <summary>
    /// Returns hidden technologies of the network that list the given item
    /// prototype as a required analysis subject and were not revealed yet.
    /// </summary>
    public List<ProtoId<TechnologyPrototype>> GetHiddenTechnologiesForRequiredItem(
        EntityUid server,
        EntityUid item,
        ResearchServerComponent? component = null)
    {
        var result = new List<ProtoId<TechnologyPrototype>>();

        if (MetaData(item).EntityPrototype is not { } prototype ||
            !TryGetNetworkAuthority(server, out _, out var authorityComponent, component) ||
            !TryComp<TechnologyDatabaseComponent>(authorityComponent.Owner, out var database))
            return result;

        foreach (var tech in ProtoMan.EnumeratePrototypes<TechnologyPrototype>())
        {
            if (!tech.Hidden ||
                !tech.RequiredItemsToUnlock.Contains(prototype.ID) ||
                database.UnlockedTechnologies.Contains(tech.ID) ||
                database.RevealedTechnologies.Contains(tech.ID))
                continue;

            result.Add(tech.ID);
        }

        return result;
    }
}
