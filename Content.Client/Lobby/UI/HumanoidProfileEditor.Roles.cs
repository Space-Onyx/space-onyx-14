using System.Linq;
using System.Numerics;
using Content.Client.Lobby.UI.Loadouts;
using Content.Client.Lobby.UI.Roles;
using Content.Client._Onyx.AlternativeJobs;
using Content.Client._Onyx.Lobby.UI.Roles;
using Content.Shared._Onyx.AlternativeJobs;
using Content.Shared.Clothing;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{

    /// <summary>
    /// Temporary override of their selected job, used to preview roles.
    /// </summary>
    public JobPrototype? JobOverride;

    // One at a time.
    private LoadoutWindow? _loadoutWindow;

    // <Onyx-RolesPersonalization-edited>
    private readonly List<(string, JobPreferenceCard)> _jobPriorities = new();
    private readonly List<(string Department, JobPreferenceCard Card)> _jobCards = new(); // <Onyx-RolesPersonalization>
    private readonly List<DepartmentPrototype> _jobFilterDepartments = new(); // <Onyx-RolesPersonalization>
    private string _jobSearch = string.Empty; // <Onyx-RolesPersonalization>
    private string? _jobDepartmentFilter; // <Onyx-RolesPersonalization>
    private bool _showSelectedJobs; // <Onyx-RolesPersonalization>

    private readonly Dictionary<string, BoxContainer> _jobCategories;
    private readonly Dictionary<string, JobGridContainer> _jobGrids = new(); // <Onyx-RolesPersonalization>
    // </Onyx-RolesPersonalization-edited>

    /// <summary>
    /// Updates selected job priorities to the profile's.
    /// </summary>
    private void UpdateJobPriorities()
    {
        foreach (var (jobId, prioritySelector) in _jobPriorities)
        {
            var priority = Profile?.JobPriorities.GetValueOrDefault(jobId, JobPriority.Never) ?? JobPriority.Never;
            prioritySelector.SelectPriority(priority); // <Onyx-RolesPersonalization-edited>
        }
    }

    private void UpdateAlternativeJobs() // <Onyx-RolesPersonalization-edited>
    {
        if (Profile is null)
            return;

        foreach (var (jobId, card) in _jobPriorities)
            card.SelectAlternative(Profile.JobAlternatives.GetValueOrDefault(jobId));
    }

    // <Onyx-RolesPersonalization>
    private void InitializeJobFilters()
    {
        JobSearchBar.OnTextChanged += args =>
        {
            _jobSearch = args.Text.Trim();
            ApplyJobFilters();
        };
        JobSearchClear.OnPressed += _ => JobSearchBar.Text = string.Empty;
        JobDepartmentFilter.OnItemSelected += args =>
        {
            JobDepartmentFilter.SelectId(args.Id);
            _jobDepartmentFilter = args.Id == 0 ? null : _jobFilterDepartments[args.Id - 1].ID;
            ApplyJobFilters();
        };
        SelectedJobsToggle.OnPressed += _ =>
        {
            _showSelectedJobs = SelectedJobsToggle.Pressed;
            ApplyJobFilters();
        };
    }

    private void ApplyJobFilters()
    {
        foreach (var (department, card) in _jobCards)
        {
            card.Visible = (_jobDepartmentFilter == null || department == _jobDepartmentFilter) &&
                           (!_showSelectedJobs || card.Priority != JobPriority.Never) &&
                           card.Matches(_jobSearch);
        }

        foreach (var (department, category) in _jobCategories)
            category.Visible = _jobCards.Any(entry => entry.Department == department && entry.Card.Visible);

        foreach (var grid in _jobGrids.Values)
            grid.Rebuild();
    }
    // </Onyx-RolesPersonalization>

    /// <summary>
    /// Refresh all loadouts.
    /// </summary>
    public void RefreshLoadouts()
    {
        _loadoutWindow?.Dispose();
        RefreshLoadoutPersonalization(); // <Onyx-LoadoutPersonalization>
    }

    private void OpenLoadout(JobPrototype? jobProto, RoleLoadout roleLoadout, RoleLoadoutPrototype roleLoadoutProto)
    {
        _loadoutWindow?.Dispose();
        _loadoutWindow = null;
        var collection = IoCManager.Instance;

        if (collection == null || _playerManager.LocalSession == null || Profile == null)
            return;

        JobOverride = jobProto;
        var session = _playerManager.LocalSession;

        _loadoutWindow = new LoadoutWindow(Profile, roleLoadout, roleLoadoutProto, _playerManager.LocalSession, collection)
        {
            Title = Loc.GetString("loadout-window-title-loadout", ("job", $"{jobProto?.LocalizedName}")),
        };

        // Refresh the buttons etc.
        _loadoutWindow.RefreshLoadouts(roleLoadout, session, collection);
        _loadoutWindow.OpenCenteredLeft();

        _loadoutWindow.OnNameChanged += name =>
        {
            roleLoadout.EntityName = name;
            Profile = Profile.WithLoadout(roleLoadout);
            SetDirty();
        };

        _loadoutWindow.OnLoadoutPressed += (loadoutGroup, loadoutProto) =>
        {
            roleLoadout.AddLoadout(loadoutGroup, loadoutProto, _prototypeManager);
            _loadoutWindow.RefreshLoadouts(roleLoadout, session, collection);
            Profile = Profile?.WithLoadout(roleLoadout);
            ReloadPreview();
        };

        _loadoutWindow.OnLoadoutUnpressed += (loadoutGroup, loadoutProto) =>
        {
            roleLoadout.RemoveLoadout(loadoutGroup, loadoutProto, _prototypeManager);
            _loadoutWindow.RefreshLoadouts(roleLoadout, session, collection);
            Profile = Profile?.WithLoadout(roleLoadout);
            ReloadPreview();
        };

        JobOverride = jobProto;
        ReloadPreview();

        _loadoutWindow.OnClose += () =>
        {
            JobOverride = null;
            ReloadPreview();
        };

        if (Profile is null)
            return;

        UpdateJobPriorities();
    }

    /// <summary>
    /// Refreshes all job selectors.
    /// </summary>
    // <Onyx-RolesPersonalization-edited>
    public void RefreshJobs()
    {
        JobList.RemoveAllChildren();
        _jobCategories.Clear();
        _jobGrids.Clear(); // <Onyx-RolesPersonalization>
        _jobPriorities.Clear();
        _jobCards.Clear(); // <Onyx-RolesPersonalization>

        // Get all displayed departments
        var departments = new List<DepartmentPrototype>();
        foreach (var department in _prototypeManager.EnumeratePrototypes<DepartmentPrototype>())
        {
            if (department.EditorHidden)
                continue;

            departments.Add(department);
        }

        departments.Sort(DepartmentUIComparer.Instance);
        _jobFilterDepartments.Clear(); // <Onyx-RolesPersonalization>
        _jobFilterDepartments.AddRange(departments); // <Onyx-RolesPersonalization>
        var selectedDepartment = _jobDepartmentFilter; // <Onyx-RolesPersonalization>
        JobDepartmentFilter.Clear(); // <Onyx-RolesPersonalization>
        JobDepartmentFilter.AddItem(Loc.GetString("job-personalization-all-departments"), 0); // <Onyx-RolesPersonalization>
        for (var i = 0; i < departments.Count; i++) // <Onyx-RolesPersonalization>
            JobDepartmentFilter.AddItem(Loc.GetString(departments[i].Name), i + 1); // <Onyx-RolesPersonalization>
        var selectedDepartmentIndex = departments.FindIndex(department => department.ID == selectedDepartment) + 1; // <Onyx-RolesPersonalization>
        JobDepartmentFilter.SelectId(selectedDepartmentIndex); // <Onyx-RolesPersonalization>
        _jobDepartmentFilter = selectedDepartmentIndex == 0 ? null : selectedDepartment; // <Onyx-RolesPersonalization>

        foreach (var department in departments)
        {
            var departmentName = Loc.GetString(department.Name);

            if (!_jobCategories.TryGetValue(department.ID, out var category))
            {
                category = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    Name = department.ID,
                    HorizontalExpand = true,
                    Margin = new Thickness(0, 0, 0, 12),
                    ToolTip = Loc.GetString("humanoid-profile-editor-jobs-amount-in-department-tooltip",
                        ("departmentName", departmentName))
                };

                category.AddChild(new PanelContainer
                {
                    PanelOverride = new StyleBoxFlat
                    {
                        BackgroundColor = Color.FromHex("#20232A"),
                        BorderColor = department.Color,
                        BorderThickness = new Thickness(0, 0, 0, 2),
                        ContentMarginLeftOverride = 8,
                        ContentMarginTopOverride = 5,
                        ContentMarginBottomOverride = 5,
                    },
                    Children =
                        {
                            new Label
                            {
                                Text = Loc.GetString("humanoid-profile-editor-department-jobs-label",
                                    ("departmentName", departmentName)),
                                FontColorOverride = department.Color,
                                StyleClasses = { "font-bold" },
                            }
                        }
                });
                var grid = new JobGridContainer
                {
                    Name = $"{department.ID}Grid",
                    Margin = new Thickness(0, 7, 0, 0),
                    HorizontalExpand = true,
                };
                category.AddChild(grid);

                _jobCategories[department.ID] = category;
                _jobGrids[department.ID] = grid; // <Onyx-RolesPersonalization>
                JobList.AddChild(category);
            }

            var jobs = department.Roles.Select(jobId => _prototypeManager.Index(jobId))
                .Where(job => job.SetPreference)
                .ToArray();

            if (JobUIComparer.TryCreate(_prototypeManager, null, out var comparer))
                Array.Sort(jobs, comparer);

            foreach (var job in jobs)
            {
                FormattedMessage? lockedReason = null; // <Onyx-RolesPersonalization>
                if (!_requirements.IsAllowed(job, (HumanoidCharacterProfile?) _preferencesManager.Preferences?.SelectedCharacter, out var reason)) // <Onyx-RolesPersonalization-edited>
                    lockedReason = reason; // <Onyx-RolesPersonalization>

                var card = new JobPreferenceCard(job, _sprite, _prototypeManager, _requirements, Profile, lockedReason); // <Onyx-RolesPersonalization>
                card.OnPrioritySelected += selectedJobPrio => // <Onyx-RolesPersonalization-edited>
                {
                    Profile = Profile?.WithJobPriority(job.ID, selectedJobPrio);

                    foreach (var (jobId, other) in _jobPriorities)
                    {
                        // Sync other selectors with the same job in case of multiple department jobs
                        if (jobId == job.ID)
                        {
                            other.SelectPriority(selectedJobPrio);
                            continue;
                        }

                        if (selectedJobPrio != JobPriority.High || other.Priority != JobPriority.High)
                            continue;

                        // Lower any other high priorities to medium.
                        other.SelectPriority(JobPriority.Medium);
                        Profile = Profile?.WithJobPriority(jobId, JobPriority.Medium);
                    }

                    // TODO: Only reload on high change (either to or from).
                    ReloadPreview();
                    RefreshLoadoutPersonalization(); // <Onyx-LoadoutPersonalization>

                    UpdateJobPriorities();
                    ApplyJobFilters(); // <Onyx-RolesPersonalization>
                    SetDirty();
                };
                card.OnAlternativeSelected += alternativeId => // <Onyx-RolesPersonalization-edited>
                {
                    Profile = Profile?.WithJobAlternative(job.ID, alternativeId);
                    foreach (var (jobId, other) in _jobPriorities)
                    {
                        if (jobId == job.ID && other != card)
                            other.SelectAlternative(alternativeId);
                    }
                    SetDirty();
                };
                _jobPriorities.Add((job.ID, card)); // <Onyx-RolesPersonalization-edited>
                _jobCards.Add((department.ID, card)); // <Onyx-RolesPersonalization>
                _jobGrids[department.ID].AddCard(card); // <Onyx-RolesPersonalization>
            }
        }

        UpdateJobPriorities();
        UpdateAlternativeJobs(); // <Onyx-AlternativeJobs>
        ApplyJobFilters(); // <Onyx-RolesPersonalization>
    }
    // </Onyx-RolesPersonalization-edited>

    public void RefreshAntags()
    {
        AntagList.RemoveAllChildren();
        var items = new[]
        {
            ("humanoid-profile-editor-antag-preference-yes-button", 0),
            ("humanoid-profile-editor-antag-preference-no-button", 1)
        };

        foreach (var antag in _prototypeManager.EnumeratePrototypes<AntagPrototype>().OrderBy(a => Loc.GetString(a.Name)))
        {
            if (!antag.SetPreference)
                continue;

            var antagContainer = new BoxContainer()
            {
                Orientation = LayoutOrientation.Horizontal,
            };

            var selector = new RequirementsSelector()
            {
                Margin = new Thickness(3f, 3f, 3f, 0f),
            };
            selector.OnOpenGuidebook += OnOpenGuidebook;

            var title = Loc.GetString(antag.Name);
            var description = Loc.GetString(antag.Objective);
            selector.Setup(items, title, 250, description, guides: antag.Guides);
            selector.Select(Profile?.AntagPreferences.Contains(antag.ID) == true ? 0 : 1);

            if (!_requirements.IsAllowed(
                    antag,
                    (HumanoidCharacterProfile?)_preferencesManager.Preferences?.SelectedCharacter,
                    out var reason))
            {
                selector.LockRequirements(reason);
                Profile = Profile?.WithAntagPreference(antag.ID, false);
                SetDirty();
            }
            else
            {
                selector.UnlockRequirements();
            }

            selector.OnSelected += preference =>
            {
                Profile = Profile?.WithAntagPreference(antag.ID, preference == 0);
                SetDirty();
            };

            antagContainer.AddChild(selector); // <Onyx-RolesPersonalization-edited>

            AntagList.AddChild(antagContainer);
        }
    }
}
