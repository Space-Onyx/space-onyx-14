using System.Linq; // <Onyx-ResearchItemRequirements>
using Content.Shared.Database;
using Content.Shared._Onyx.Research; // <Onyx-ResearchNetworks>
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server.Research.Systems;

public sealed partial class ResearchSystem
{
    /// <summary>
    /// Syncs the primary entity's database to that of the secondary entity's database.
    /// </summary>
    public void Sync(EntityUid primaryUid, EntityUid otherUid, TechnologyDatabaseComponent? primaryDb = null, TechnologyDatabaseComponent? otherDb = null)
    {
        if (!Resolve(primaryUid, ref primaryDb) || !Resolve(otherUid, ref otherDb))
            return;

        primaryDb.MainDiscipline = otherDb.MainDiscipline;
        // <Onyx-ResearchNetworks-edited>
        primaryDb.CurrentTechnologyCards = new(otherDb.CurrentTechnologyCards);
        primaryDb.SupportedDisciplines = new(otherDb.SupportedDisciplines);
        primaryDb.UnlockedTechnologies = new(otherDb.UnlockedTechnologies);
        primaryDb.RevealedTechnologies = new(otherDb.RevealedTechnologies); // <Onyx-TechDiscovery>
        // <Onyx-ResearchItemRequirements>
        primaryDb.CompletedRevealRequirements = otherDb.CompletedRevealRequirements
            .ToDictionary(pair => pair.Key, pair => new Dictionary<int, int>(pair.Value));
        primaryDb.CompletedResearchRequirements = otherDb.CompletedResearchRequirements
            .ToDictionary(pair => pair.Key, pair => new Dictionary<int, int>(pair.Value));
        // </Onyx-ResearchItemRequirements>
        primaryDb.UnlockedRecipes = new(otherDb.UnlockedRecipes);
        // </Onyx-ResearchNetworks-edited>

        Dirty(primaryUid, primaryDb);

        var ev = new TechnologyDatabaseSynchronizedEvent();
        RaiseLocalEvent(primaryUid, ref ev);
    }

    /// <summary>
    ///     If there's a research client component attached to the owner entity,
    ///     and the research client is connected to a research server, this method
    ///     syncs against the research server, and the server against the local database.
    /// </summary>
    /// <returns>Whether it could sync or not</returns>
    public void SyncClientWithServer(EntityUid uid, TechnologyDatabaseComponent? databaseComponent = null, ResearchClientComponent? clientComponent = null)
    {
        if (!Resolve(uid, ref databaseComponent, ref clientComponent, false))
            return;

        // <Onyx-ResearchNetworks-edited>
        if (!TryGetClientServer(uid, out var authority, out _, clientComponent) ||
            !TryComp<TechnologyDatabaseComponent>(authority, out var serverDatabase))
            return;

        Sync(uid, authority.Value, databaseComponent, serverDatabase);
        // </Onyx-ResearchNetworks-edited>
    }

    /// <summary>
    /// Tries to add a technology to a database, checking if it is able to
    /// </summary>
    /// <returns>If the technology was successfully added</returns>
    public bool UnlockTechnology(EntityUid client,
        string prototypeid,
        EntityUid user,
        ResearchClientComponent? component = null,
        TechnologyDatabaseComponent? clientDatabase = null)
    {
        if (!ProtoMan.TryIndex<TechnologyPrototype>(prototypeid, out var prototype))
            return false;

        return UnlockTechnology(client, prototype, user, component, clientDatabase);
    }

    /// <summary>
    /// Tries to add a technology to a database, checking if it is able to
    /// </summary>
    /// <returns>If the technology was successfully added</returns>
    public bool UnlockTechnology(EntityUid client,
        TechnologyPrototype prototype,
        EntityUid user,
        ResearchClientComponent? component = null,
        TechnologyDatabaseComponent? clientDatabase = null)
    {
        if (!Resolve(client, ref component, ref clientDatabase, false))
            return false;

        if (!TryGetClientServer(client, out var serverEnt, out _, component))
            return false;

        if (!CanServerUnlockTechnology(client, prototype, out var finalCosts, clientDatabase, component)) // <Onyx-ResearchPointTypes-edited>
            return false;

        AddTechnology(serverEnt.Value, prototype);
        // <Onyx-FancyResearchUI-edited>
        // Research disciplines no longer lock each other out.
        // TrySetMainDiscipline(prototype, serverEnt.Value);
        // </Onyx-FancyResearchUI-edited>
        TryConsumePoints(serverEnt.Value, finalCosts); // <Onyx-ResearchPointTypes-edited>
        UpdateTechnologyCards(serverEnt.Value);
        // <Onyx-FancyResearchUI>
        // AddTechnology raises its event before cards are regenerated; notify peer consoles of the final state.
        var cardsUpdated = new TechnologyDatabaseModifiedEvent(null);
        RaiseLocalEvent(serverEnt.Value, ref cardsUpdated);
        // </Onyx-FancyResearchUI>

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):player} unlocked {prototype.ID} (discipline: {prototype.Discipline}, tier: {prototype.Tier}) at {ToPrettyString(client)}, for server {ToPrettyString(serverEnt.Value)}.");
        // <Onyx-ResearchNetworks>
        LogNetworkEvent(serverEnt.Value, ResearchNetworkLogType.TechnologyUnlocked,
            Loc.GetString("research-network-log-technology-unlocked",
                ("technology", Loc.GetString(prototype.Name)),
                ("user", GetResearchLogUserName(user))));
        // </Onyx-ResearchNetworks>
        return true;
    }

    /// <summary>
    ///     Adds a technology to the database without checking if it could be unlocked.
    /// </summary>
    [PublicAPI]
    public void AddTechnology(EntityUid uid, string technology, TechnologyDatabaseComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!ProtoMan.TryIndex<TechnologyPrototype>(technology, out var prototype))
            return;
        AddTechnology(uid, prototype, component);
    }

    /// <summary>
    ///     Adds a technology to the database without checking if it could be unlocked.
    /// </summary>
    public void AddTechnology(EntityUid uid, TechnologyPrototype technology, TechnologyDatabaseComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        //todo this needs to support some other stuff, too
        foreach (var generic in technology.GenericUnlocks)
        {
            if (generic.PurchaseEvent != null)
                RaiseLocalEvent(generic.PurchaseEvent);
        }

        component.UnlockedTechnologies.Add(technology.ID);
        var addedRecipes = new List<string>();
        foreach (var unlock in technology.RecipeUnlocks)
        {
            if (component.UnlockedRecipes.Contains(unlock))
                continue;
            component.UnlockedRecipes.Add(unlock);
            addedRecipes.Add(unlock);
        }
        Dirty(uid, component);

        var ev = new TechnologyDatabaseModifiedEvent(addedRecipes);
        RaiseLocalEvent(uid, ref ev);
    }

    /// <summary>
    ///     Returns whether a technology can be unlocked on this database,
    ///     taking parent technologies into account.
    /// </summary>
    /// <returns>Whether it could be unlocked or not</returns>
    public bool CanServerUnlockTechnology(EntityUid uid,
        TechnologyPrototype technology,
        TechnologyDatabaseComponent? database = null,
        ResearchClientComponent? client = null)
    {
        // <Onyx-ResearchPointTypes-edited>
        // Typed point balances are authoritative; see the out-parameter overload.
        return CanServerUnlockTechnology(uid, technology, out _, database, client);
        // </Onyx-ResearchPointTypes-edited>
    }

    private void OnDatabaseRegistrationChanged(EntityUid uid, TechnologyDatabaseComponent component, ref ResearchRegistrationChangedEvent args)
    {
        if (args.Server != null)
            return;
        component.MainDiscipline = null;
        component.CurrentTechnologyCards = new List<string>();
        component.SupportedDisciplines = new List<ProtoId<TechDisciplinePrototype>>();
        component.UnlockedTechnologies = new List<ProtoId<TechnologyPrototype>>();
        component.RevealedTechnologies = new List<ProtoId<TechnologyPrototype>>(); // <Onyx-TechDiscovery>
        component.CompletedRevealRequirements = new(); // <Onyx-ResearchItemRequirements>
        component.CompletedResearchRequirements = new(); // <Onyx-ResearchItemRequirements>
        component.UnlockedRecipes = new List<ProtoId<LatheRecipePrototype>>();
        Dirty(uid, component);
    }
}
