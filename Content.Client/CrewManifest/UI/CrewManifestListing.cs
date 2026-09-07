using Content.Shared.CrewManifest;
using Content.Shared.Roles;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.CrewManifest.UI;

public sealed partial class CrewManifestListing : BoxContainer
{
    [Dependency] private IEntitySystemManager _entitySystem = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    private readonly SpriteSystem _spriteSystem;

    public CrewManifestListing()
    {
        IoCManager.InjectDependencies(this);
        _spriteSystem = _entitySystem.GetEntitySystem<SpriteSystem>();
    }

    public void AddCrewManifestEntries(CrewManifestEntries entries, string filter = "", bool stripedRows = false) // <Onyx-CrewManifest-edited>
    {
        var entryDict = new Dictionary<DepartmentPrototype, List<CrewManifestEntry>>();
        filter = filter.Trim(); // <Onyx-CrewManifest>

        foreach (var entry in entries.Entries)
        {
            // <Onyx-CrewManifest>
            if (!string.IsNullOrEmpty(filter)
                && !entry.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
                && !entry.JobTitle.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
                continue;
            // </Onyx-CrewManifest>

            foreach (var department in _prototypeManager.EnumeratePrototypes<DepartmentPrototype>())
            {
                // this is a little expensive, and could be better
                if (department.Roles.Contains(entry.JobPrototype))
                {
                    entryDict.GetOrNew(department).Add(entry);
                }
            }
        }

        var entryList = new List<(DepartmentPrototype section, List<CrewManifestEntry> entries)>();

        foreach (var (section, listing) in entryDict)
        {
            entryList.Add((section, listing));
        }

        entryList.Sort((a, b) => DepartmentUIComparer.Instance.Compare(a.section, b.section));

        foreach (var item in entryList)
        {
            AddChild(new CrewManifestSection(_prototypeManager, _spriteSystem, item.section, item.entries, stripedRows)); // <Onyx-CrewManifest-edited>
        }
    }
}
