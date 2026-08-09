using System.Linq;
using Content.Client._Onyx.Lobby.UI.Loadouts;
using Content.Client._Onyx.Loadouts;
using Content.Corvax.Interfaces.Shared;
using Content.Shared.CCVar;
using Content.Shared.Clothing;
using Content.Shared.Inventory;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private string _loadoutSearch = string.Empty;
    private ProtoId<RoleLoadoutPrototype>? _selectedLoadoutRole;
    private bool _showSelectedLoadouts;

    private void InitializeLoadoutPersonalization()
    {
        LoadoutSearchBar.PlaceHolder = Loc.GetString("loadout-search-placeholder");
        LoadoutSearchBar.OnTextChanged += args =>
        {
            _loadoutSearch = args.Text.Trim();
            RefreshLoadoutPersonalization();
        };
        LoadoutSearchClear.OnPressed += _ => LoadoutSearchBar.Text = string.Empty;
        SelectedLoadoutsToggle.OnPressed += _ =>
        {
            _showSelectedLoadouts = SelectedLoadoutsToggle.Pressed;
            RefreshLoadoutPersonalization();
        };
        LoadoutRoleSelector.OnItemSelected += args =>
        {
            _selectedLoadoutRole = args.Id >= 0 && args.Id < _loadoutRoles.Count
                ? _loadoutRoles[args.Id]
                : (ProtoId<RoleLoadoutPrototype>?) null;
            UpdateLoadoutRoleIcon(args.Id);
            RefreshLoadoutPersonalization();
        };
        LoadoutRoleSelector.StyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#182535"),
            BorderColor = Color.FromHex("#4c7da8"),
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 12,
            ContentMarginRightOverride = 10,
            ContentMarginTopOverride = 7,
            ContentMarginBottomOverride = 7,
        };
        UpdateSelectedLoadoutsToggle(0);
    }

    private readonly List<ProtoId<RoleLoadoutPrototype>> _loadoutRoles = new();
    private readonly List<JobPrototype> _loadoutRoleJobs = new();

    private void DisposeLoadoutPersonalization()
    {
        LoadoutSlotTabs.DisposeAllChildren();
    }

    private void RefreshLoadoutPersonalization()
    {
        LoadoutSlotTabs.DisposeAllChildren();

        if (Profile == null || _playerManager.LocalSession == null)
            return;

        RefreshLoadoutRoleSelector();
        var roleId = _selectedLoadoutRole ?? GetActiveLoadoutRole();
        if (roleId == null || !_prototypeManager.TryIndex<RoleLoadoutPrototype>(roleId, out var roleProto))
            return;

        var loadout = Profile.Loadouts.TryGetValue(roleId, out var existing)
            ? existing.Clone()
            : new RoleLoadout(roleId.Value);
        loadout.SetDefault(Profile, _playerManager.LocalSession, _prototypeManager);

        var collection = IoCManager.Instance;
        if (collection == null)
            return;

        var loadoutSystem = _entManager.System<LoadoutSystem>();
        var groups = roleProto.Groups
            .Select(groupId => _prototypeManager.TryIndex(groupId, out LoadoutGroupPrototype? group) ? group : null)
            .Where(group => group is { Hidden: false })
            .Cast<LoadoutGroupPrototype>()
            .ToList();
        UpdateSelectedLoadoutsToggle(loadout.SelectedLoadouts.Values.Sum(items => items.Count));

        AddRoleCustomization(roleId.Value, roleProto, loadout);

        if (_showSelectedLoadouts)
        {
            AddSelectedLoadoutsView(roleId.Value, groups, loadout, collection, loadoutSystem);
            return;
        }

        foreach (var group in groups)
        {

            var body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(5) };
            var choices = GetGroupLoadouts(group, collection)
                .Where(id => _prototypeManager.TryIndex(id, out LoadoutPrototype? _))
                .Select(id => _prototypeManager.Index<LoadoutPrototype>(id))
                .Where(proto => MatchesLoadoutSearch(proto, loadoutSystem))
                .OrderBy(loadoutSystem.GetName)
                .ToList();

            if (choices.Count == 0)
                continue;

            var selectedCount = loadout.SelectedLoadouts.GetValueOrDefault(group.ID)?.Count ?? 0;
            body.AddChild(new Label
            {
                Text = Loc.GetString("loadout-group-limit", ("selected", selectedCount), ("max", group.MaxLimit)),
                Margin = new Thickness(0, 0, 0, 5),
                StyleClasses = { "LabelKeyText" },
            });

            var tiles = new LoadoutWrapContainer { HorizontalExpand = true, Separation = 6 };
            foreach (var prototype in choices)
                tiles.AddChild(CreateLoadoutTile(roleId.Value, group, loadout, prototype, collection, loadoutSystem));
            body.AddChild(tiles);

            if (body.ChildCount == 0)
                continue;

            var scroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true, HScrollEnabled = false, Children = { body } };
            LoadoutSlotTabs.AddChild(scroll);
            LoadoutSlotTabs.SetTabTitle(LoadoutSlotTabs.ChildCount - 1, Loc.GetString(group.Name));
        }

    }

    private void AddRoleCustomization(ProtoId<RoleLoadoutPrototype> role, RoleLoadoutPrototype roleProto, RoleLoadout loadout)
    {
        if (!roleProto.CanCustomizeName)
            return;

        var name = new LineEdit
        {
            Text = loadout.EntityName ?? string.Empty,
            HorizontalExpand = true,
            ToolTip = Loc.GetString("loadout-name-edit-tooltip", ("max", _cfgManager.GetCVar(CCVars.MaxLoadoutNameLength))),
        };
        name.IsValid = text => text.Length <= _cfgManager.GetCVar(CCVars.MaxLoadoutNameLength);
        name.OnTextChanged += args => SetRoleLoadoutName(role, args.Text);

        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(10),
            Children =
            {
                new Label
                {
                    Text = Loc.GetString(roleProto.NameDataset == null ? "loadout-name-edit-label" : "loadout-name-edit-label-dataset"),
                    HorizontalExpand = true,
                },
                name,
            },
        };
        LoadoutSlotTabs.AddChild(body);
        LoadoutSlotTabs.SetTabTitle(LoadoutSlotTabs.ChildCount - 1, Loc.GetString("loadout-customization-tab"));
    }

    private void SetRoleLoadoutName(ProtoId<RoleLoadoutPrototype> role, string name)
    {
        if (Profile == null || name.Length > _cfgManager.GetCVar(CCVars.MaxLoadoutNameLength))
            return;

        var loadout = Profile.Loadouts.TryGetValue(role, out var existing) ? existing.Clone() : new RoleLoadout(role);
        loadout.EntityName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        Profile = Profile.WithLoadout(loadout);
        SetDirty();
        ReloadPreview();
    }

    private void AddSelectedLoadoutsView(ProtoId<RoleLoadoutPrototype> role, List<LoadoutGroupPrototype> groups, RoleLoadout loadout, IDependencyCollection collection, LoadoutSystem system)
    {
        var body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(5) };
        foreach (var group in groups)
        {
            if (!loadout.SelectedLoadouts.TryGetValue(group.ID, out var selected))
                continue;

            var prototypes = selected
                .Where(item => _prototypeManager.TryIndex(item.Prototype, out LoadoutPrototype? _))
                .Select(item => _prototypeManager.Index<LoadoutPrototype>(item.Prototype))
                .Where(prototype => MatchesLoadoutSearch(prototype, system))
                .ToList();
            if (prototypes.Count == 0)
                continue;

            body.AddChild(new Label
            {
                Text = Loc.GetString(group.Name),
                FontColorOverride = Color.FromHex("#8bc5ff"),
                StyleClasses = { "font-bold" },
                Margin = new Thickness(0, 8, 0, 5),
            });
            var tiles = new LoadoutWrapContainer { HorizontalExpand = true, Separation = 6 };
            foreach (var prototype in prototypes)
                tiles.AddChild(CreateLoadoutTile(role, group, loadout, prototype, collection, system));
            body.AddChild(tiles);
        }

        if (body.ChildCount == 0)
            body.AddChild(new Label { Text = Loc.GetString("loadout-selected-empty") });

        LoadoutSlotTabs.AddChild(new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
            Children = { body },
        });
        LoadoutSlotTabs.SetTabTitle(0, Loc.GetString("loadout-selected-filter-tab"));
    }

    private ProtoId<RoleLoadoutPrototype>? GetActiveLoadoutRole()
    {
        if (Profile == null)
            return null;

        var job = Profile.JobPriorities.FirstOrDefault(priority => priority.Value == JobPriority.High).Key;
        var jobId = job.Id ?? SharedGameTicker.FallbackOverflowJob;
        var role = LoadoutSystem.GetJobPrototype(jobId);
        return _prototypeManager.HasIndex<RoleLoadoutPrototype>(role) ? role : null;
    }

    private void RefreshLoadoutRoleSelector()
    {
        var activeRole = GetActiveLoadoutRole();
        var selected = _selectedLoadoutRole ?? activeRole;
        _loadoutRoles.Clear();
        _loadoutRoleJobs.Clear();
        LoadoutRoleSelector.Clear();

        foreach (var job in _prototypeManager.EnumeratePrototypes<JobPrototype>()
                     .Where(job => job.SetPreference)
                     .OrderBy(job => job.LocalizedName))
        {
            var role = LoadoutSystem.GetJobPrototype(job.ID);
            if (!_prototypeManager.HasIndex<RoleLoadoutPrototype>(role) || _loadoutRoles.Contains(role))
                continue;

            _loadoutRoles.Add(role);
            _loadoutRoleJobs.Add(job);
            var icon = _prototypeManager.Index(job.Icon);
            LoadoutRoleSelector.AddJob(job.LocalizedName, _sprite.Frame0(icon.Icon), _loadoutRoles.Count - 1);
        }

        var index = selected == null ? -1 : _loadoutRoles.FindIndex(role => role == selected.Value);
        if (index < 0 && _loadoutRoles.Count > 0)
        {
            index = 0;
            _selectedLoadoutRole = _loadoutRoles[index];
        }

        if (index >= 0)
        {
            LoadoutRoleSelector.SelectId(index);
            UpdateLoadoutRoleIcon(index);
        }
    }

    private void UpdateLoadoutRoleIcon(int index)
    {
        if (index < 0 || index >= _loadoutRoleJobs.Count)
        {
            LoadoutRoleSelector.SetSelectedIcon(null);
            return;
        }

        var icon = _prototypeManager.Index(_loadoutRoleJobs[index].Icon);
        LoadoutRoleSelector.SetSelectedIcon(_sprite.Frame0(icon.Icon));
    }

    private IEnumerable<ProtoId<LoadoutPrototype>> GetGroupLoadouts(LoadoutGroupPrototype group, IDependencyCollection collection)
    {
        if (group.ID == "Inventory" && collection.TryResolveType<ISharedLoadoutsManager>(out var manager))
            return manager.GetClientPrototypes().Select(id => (ProtoId<LoadoutPrototype>) id);

        return group.Loadouts;
    }

    private bool MatchesLoadoutSearch(LoadoutPrototype prototype, LoadoutSystem system)
    {
        if (string.IsNullOrEmpty(_loadoutSearch))
            return true;

        var query = _loadoutSearch;
        if (system.GetName(prototype).Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        var entity = prototype.DummyEntity ?? system.GetFirstOrNull(prototype);
        return entity != null && _prototypeManager.TryIndex<EntityPrototype>(entity, out var entityProto) &&
               (entityProto.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                entityProto.Description.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private LoadoutIconButton CreateLoadoutTile(
        ProtoId<RoleLoadoutPrototype> role,
        LoadoutGroupPrototype group,
        RoleLoadout loadout,
        LoadoutPrototype prototype,
        IDependencyCollection collection,
        LoadoutSystem system)
    {
        var selected = loadout.SelectedLoadouts.GetValueOrDefault(group.ID)?.FirstOrDefault(item => item.Prototype == prototype.ID);
        var enabled = loadout.IsValid(Profile!, _playerManager.LocalSession, prototype.ID, collection, out var reason);
        var icon = new LoadoutIconButton(prototype, selected?.CustomName ?? system.GetName(prototype), selected?.CustomColorTint, enabled ? null : reason)
        {
            Pressed = selected != null,
            Disabled = !enabled,
        };
        icon.OnPressed += args => SetPersonalizedLoadout(role, group.ID, prototype, args.Button.Pressed);
        icon.OnCustomizePressed += (_, _) => OpenLoadoutCustomization(role, group.ID, prototype, selected, system.GetName(prototype));
        return icon;
    }

    private void SetPersonalizedLoadout(ProtoId<RoleLoadoutPrototype> role, ProtoId<LoadoutGroupPrototype> group, LoadoutPrototype prototype, bool selected)
    {
        if (Profile == null || _playerManager.LocalSession == null)
            return;

        var loadout = Profile.Loadouts.TryGetValue(role, out var existing) ? existing.Clone() : new RoleLoadout(role);
        loadout.SetDefault(Profile, _playerManager.LocalSession, _prototypeManager);
        if (selected)
        {
            if (!loadout.SelectedLoadouts.ContainsKey(group))
                loadout.SelectedLoadouts[group] = new List<Loadout>();
            RemoveConflictingLoadouts(loadout, group, prototype);
            loadout.AddLoadout(group, prototype.ID, _prototypeManager);
        }
        else
        {
            loadout.RemoveLoadout(group, prototype.ID, _prototypeManager);
        }

        Profile = Profile.WithLoadout(loadout);
        SetDirty();
        RefreshLoadoutPersonalization();
        ReloadPreview();
    }

    private void OpenLoadoutCustomization(ProtoId<RoleLoadoutPrototype> role, ProtoId<LoadoutGroupPrototype> group, LoadoutPrototype prototype, Loadout? selected, string title)
    {
        var entity = prototype.DummyEntity ?? _entManager.System<LoadoutSystem>().GetFirstOrNull(prototype);
        var defaultName = title;
        var defaultDescription = string.Empty;
        if (entity != null)
        {
            var preview = _entManager.SpawnEntity(entity, Robust.Shared.Map.MapCoordinates.Nullspace);
            if (_entManager.TryGetComponent(preview, out MetaDataComponent? metadata))
            {
                defaultName = metadata.EntityName;
                defaultDescription = metadata.EntityDescription;
            }
            _entManager.DeleteEntity(preview);
        }

        var savedColor = Color.FromHex(selected?.CustomColorTint ?? Color.White.ToHex());
        var window = new LoadoutCustomizeWindow(title, selected?.CustomName ?? defaultName, selected?.CustomDescription ?? defaultDescription, prototype.CustomColorTint ? savedColor : null);
        window.OnSubmitted += (name, description, color) =>
        {
            SetPersonalizedLoadoutCustomization(role, group, prototype, NormalizeLoadoutText(name, defaultName), NormalizeLoadoutText(description, defaultDescription), color?.ToHex());
        };
        if (prototype.CustomColorTint)
        {
            window.OnColorPreview += color => PreviewLoadoutTint(prototype, color);
            window.OnReverted += () => PreviewLoadoutTint(prototype, savedColor);
        }
        window.OpenCentered();
    }

    private void SetPersonalizedLoadoutCustomization(ProtoId<RoleLoadoutPrototype> role, ProtoId<LoadoutGroupPrototype> group, LoadoutPrototype prototype, string? name, string? description, string? tint)
    {
        if (Profile == null)
            return;

        var loadout = Profile.Loadouts.TryGetValue(role, out var existing) ? existing.Clone() : new RoleLoadout(role);
        loadout.SetDefault(Profile, _playerManager.LocalSession, _prototypeManager);
        if (!loadout.SelectedLoadouts.TryGetValue(group, out var selected))
        {
            selected = new List<Loadout>();
            loadout.SelectedLoadouts[group] = selected;
        }

        var item = selected.FirstOrDefault(entry => entry.Prototype == prototype.ID);
        if (item == null)
        {
            RemoveConflictingLoadouts(loadout, group, prototype);
            loadout.AddLoadout(group, prototype.ID, _prototypeManager);
            item = selected.First(entry => entry.Prototype == prototype.ID);
        }

        item.CustomName = name;
        item.CustomDescription = description;
        item.CustomColorTint = prototype.CustomColorTint ? tint : null;
        Profile = Profile.WithLoadout(loadout);
        SetDirty();
        RefreshLoadoutPersonalization();
        ReloadPreview();
    }

    private static string? NormalizeLoadoutText(string value, string defaultValue)
    {
        var text = value.Trim();
        return string.IsNullOrEmpty(text) || text == defaultValue.Trim() ? null : text;
    }

    private void PreviewLoadoutTint(LoadoutPrototype prototype, Color color)
    {
        if (!_entManager.EntityExists(SpriteView.PreviewDummy))
            return;

        var inventory = _entManager.System<InventorySystem>();
        var tint = _entManager.System<LoadoutTintSystem>();
        foreach (var slot in prototype.Equipment.Keys)
        {
            if (inventory.TryGetSlotEntity(SpriteView.PreviewDummy, slot, out var item))
                tint.SetTint(item.Value, color);
        }
    }

    private void RemoveConflictingLoadouts(RoleLoadout loadout, ProtoId<LoadoutGroupPrototype> group, LoadoutPrototype selected)
    {
        if (selected.Equipment.Count == 0)
            return;

        var slots = selected.Equipment.Keys.ToHashSet();
        foreach (var entries in loadout.SelectedLoadouts.Values)
            entries.RemoveAll(item => _prototypeManager.TryIndex(item.Prototype, out LoadoutPrototype? prototype) && prototype.Equipment.Keys.Any(slots.Contains));
    }

    private void UpdateSelectedLoadoutsToggle(int count)
    {
        SelectedLoadoutsToggle.Text = Loc.GetString(
            _showSelectedLoadouts ? "loadout-selected-filter-active" : "loadout-selected-filter",
            ("count", count));
        SelectedLoadoutsToggle.Pressed = _showSelectedLoadouts;
        SelectedLoadoutsToggle.StyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = _showSelectedLoadouts ? Color.FromHex("#26445e") : Color.FromHex("#1c2b3a"),
            BorderColor = Color.FromHex("#4c7da8"),
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 8,
            ContentMarginRightOverride = 8,
        };
    }
}
