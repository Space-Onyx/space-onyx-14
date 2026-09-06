using System.Linq;
using Content.Client.Research;
using Content.Shared._Onyx.Research.Prototypes;
using Content.Shared.Lathe;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._Onyx.Research.UI;

public enum ResearchAvailability : byte
{
    Researched,
    Available,
    PrereqsMet,
    Unavailable
}

[UsedImplicitly]
public sealed class FancyResearchConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private FancyResearchConsoleMenu? _consoleMenu;

    public FancyResearchConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _consoleMenu = this.CreateWindow<FancyResearchConsoleMenu>();
        _consoleMenu.SetEntity(Owner);
        _consoleMenu.OnClose += () => _consoleMenu = null;
        _consoleMenu.OnTechnologyCardPressed += id => SendMessage(new ConsoleUnlockTechnologyMessage(id));
        _consoleMenu.OnServerButtonPressed += () => SendMessage(new ConsoleServerSelectionMessage());

        if (State is ResearchConsoleBoundInterfaceState state)
            UpdateMenu(state);
    }

    public override void OnProtoReload(PrototypesReloadedEventArgs args)
    {
        base.OnProtoReload(args);
        if ((args.WasModified<TechnologyPrototype>() ||
             args.WasModified<ResearchExperimentPrototype>() ||
             args.WasModified<TechDisciplinePrototype>() ||
             args.WasModified<LatheRecipePrototype>()) &&
            State is ResearchConsoleBoundInterfaceState state)
            UpdateMenu(state, true);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ResearchConsoleBoundInterfaceState researchState)
            UpdateMenu(researchState);
    }

    private void UpdateMenu(ResearchConsoleBoundInterfaceState state, bool force = false)
    {
        if (_consoleMenu == null || !EntMan.TryGetComponent(Owner, out TechnologyDatabaseComponent? database))
            return;

        var research = EntMan.System<ResearchSystem>();
        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        var list = new Dictionary<string, ResearchAvailability>();
        foreach (var tech in prototypes.EnumeratePrototypes<TechnologyPrototype>())
        {
            var unlocked = IsUnlocked(database, tech.ID);

            if (tech.EditorDeleted ||
                !SupportsDiscipline(database, tech.Discipline) ||
                IsConcealedByHiddenTechnology(tech, database, prototypes))
                continue;

            list[tech.ID] = unlocked
                ? ResearchAvailability.Researched
                : research.IsTechnologyAvailable(database, tech)
                    ? research.CanAffordTechnology(state.PointBalances, tech, database)
                        ? ResearchAvailability.Available
                        : ResearchAvailability.PrereqsMet
                    : ResearchAvailability.Unavailable;
        }

        if (force || !_consoleMenu.List.OrderBy(x => x.Key).SequenceEqual(list.OrderBy(x => x.Key)))
            _consoleMenu.UpdatePanels(list);
        _consoleMenu.UpdateInformationPanel(state.Points, state.PointBalances);
        _consoleMenu.UpdateNetworkLogs(state.Logs);
    }

    private static bool IsConcealedByHiddenTechnology(
        TechnologyPrototype technology,
        TechnologyDatabaseComponent database,
        IPrototypeManager prototypes)
    {
        return IsConcealedByHiddenTechnology(technology, database, prototypes, new());
    }

    private static bool IsConcealedByHiddenTechnology(
        TechnologyPrototype technology,
        TechnologyDatabaseComponent database,
        IPrototypeManager prototypes,
        HashSet<string> visited)
    {
        if (!visited.Add(technology.ID))
            return false;

        if (technology.Hidden &&
            !IsUnlocked(database, technology.ID) &&
            !IsRevealed(database, technology.ID))
            return true;

        foreach (var prerequisiteId in technology.TechnologyPrerequisites)
        {
            if (prototypes.TryIndex(prerequisiteId, out TechnologyPrototype? prerequisite) &&
                IsConcealedByHiddenTechnology(prerequisite, database, prototypes, visited))
                return true;
        }

        return false;
    }

    private static bool SupportsDiscipline(TechnologyDatabaseComponent database, string discipline)
    {
        foreach (var supported in database.SupportedDisciplines)
        {
            if (supported == discipline)
                return true;
        }

        return false;
    }

    private static bool IsUnlocked(TechnologyDatabaseComponent database, string technology)
    {
        foreach (var unlocked in database.UnlockedTechnologies)
        {
            if (unlocked == technology)
                return true;
        }

        return false;
    }

    private static bool IsRevealed(TechnologyDatabaseComponent database, string technology)
    {
        foreach (var revealed in database.RevealedTechnologies)
        {
            if (revealed == technology)
                return true;
        }

        return false;
    }

}
