using System.Linq;
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
    /// Returns incomplete requirements that accept the given item prototype.
    /// </summary>
    public List<(ProtoId<TechnologyPrototype> Technology, int Requirement, bool Reveals, int Progress, int Amount)> GetTechnologyRequirementsForItem(
        EntityUid server,
        EntityUid item,
        ResearchServerComponent? component = null)
    {
        var result = new List<(ProtoId<TechnologyPrototype>, int, bool, int, int)>();

        if (MetaData(item).EntityPrototype is not { } prototype ||
            !TryGetNetworkAuthority(server, out var authority, out _, component) ||
            !TryComp<TechnologyDatabaseComponent>(authority, out var database))
            return result;

        foreach (var tech in ProtoMan.EnumeratePrototypes<TechnologyPrototype>())
        {
            if (database.UnlockedTechnologies.Contains(tech.ID))
                continue;

            if (!database.RevealedTechnologies.Contains(tech.ID))
                AddMatchingRequirements(result, tech, tech.RevealRequirements, database.CompletedRevealRequirements, prototype.ID, true);

            AddMatchingRequirements(result, tech, tech.ResearchRequirements, database.CompletedResearchRequirements, prototype.ID, false);
        }

        return result;
    }

    /// <summary>
    /// Completes one item requirement and reveals the technology after all requirements are met.
    /// </summary>
    public bool CompleteItemRequirement(
        EntityUid server,
        ProtoId<TechnologyPrototype> technology,
        int requirement,
        bool reveals,
        out bool revealed,
        out int progress,
        out int amount)
    {
        revealed = false;
        progress = 0;
        amount = 0;
        if (!TryGetNetworkAuthority(server, out var authority, out var authorityComponent) ||
            !TryComp<TechnologyDatabaseComponent>(authority, out var database) ||
            !ProtoMan.TryIndex<TechnologyPrototype>(technology, out var prototype) ||
            database.UnlockedTechnologies.Contains(technology))
            return false;

        var requirements = reveals ? prototype.RevealRequirements : prototype.ResearchRequirements;
        var completedByTechnology = reveals
            ? database.CompletedRevealRequirements
            : database.CompletedResearchRequirements;
        if (requirement < 0 || requirement >= requirements.Count ||
            (reveals && database.RevealedTechnologies.Contains(technology)))
            return false;

        if (!completedByTechnology.TryGetValue(technology, out var completed))
            completedByTechnology[technology] = completed = new();

        var requiredAmount = Math.Max(1, requirements[requirement].Amount);
        completed[requirement] = Math.Min(requiredAmount, completed.GetValueOrDefault(requirement) + 1);
        progress = completed[requirement];
        amount = requiredAmount;

        if (reveals && requirements.Select((itemRequirement, index) =>
                completed.GetValueOrDefault(index) >= Math.Max(1, itemRequirement.Amount)).All(done => done))
        {
            revealed = RevealTechnology(authority, technology);
            return revealed;
        }

        Dirty(authority, database);
        SynchronizeNetwork(authority, authorityComponent);
        var ev = new TechnologyDatabaseModifiedEvent(null);
        RaiseLocalEvent(authority, ref ev);
        return true;
    }

    private void AddMatchingRequirements(
        List<(ProtoId<TechnologyPrototype> Technology, int Requirement, bool Reveals, int Progress, int Amount)> result,
        TechnologyPrototype technology,
        List<ResearchItemRequirement> requirements,
        Dictionary<ProtoId<TechnologyPrototype>, Dictionary<int, int>> completedByTechnology,
        EntProtoId item,
        bool reveals)
    {
        completedByTechnology.TryGetValue(technology.ID, out var completed);
        var itemIds = ProtoMan.EnumerateParents<EntityPrototype>(item, true).Select(parent => parent.ID).ToHashSet();
        for (var i = 0; i < requirements.Count; i++)
        {
            if ((completed?.GetValueOrDefault(i) ?? 0) >= Math.Max(1, requirements[i].Amount) ||
                !requirements[i].AnyOf.Any(alternative => itemIds.Contains(alternative)))
                continue;

            result.Add((technology.ID, i, reveals, completed?.GetValueOrDefault(i) ?? 0, Math.Max(1, requirements[i].Amount)));
        }
    }
}
