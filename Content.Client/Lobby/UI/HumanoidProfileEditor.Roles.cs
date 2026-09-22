using System.Linq;
using System.Numerics;
using Content.Client.Lobby.UI.Loadouts;
using Content.Client.Lobby.UI.Roles;
using Content.Client._Onyx.Lobby.UI.Roles; // <Onyx-RolesPersonalization>
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

    private readonly List<(string, JobPreferenceCard)> _jobPriorities = new(); // <Onyx-RolesPersonalization-edited>

    private readonly Dictionary<string, BoxContainer> _jobCategories;

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
        // <Onyx-CharacterPersonalizationFix>
        if (Profile == null)
            return;
        // </Onyx-CharacterPersonalizationFix>
        JobList.RemoveAllChildren();
        _jobCategories.Clear();
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
        var alternativesByJob = GroupAlternativesByJob(); // <Onyx-RolesPersonalization>
        RefreshJobDepartmentFilter(departments); // <Onyx-RolesPersonalization>

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
                    SeparationOverride = 5,
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
                _jobCategories[department.ID] = category;
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
                if (!_requirements.IsAllowed(job, (HumanoidCharacterProfile?)_preferencesManager.Preferences?.SelectedCharacter, out var reason))
                    lockedReason = reason; // <Onyx-RolesPersonalization>

                alternativesByJob.TryGetValue(job.ID, out var jobAlternatives); // <Onyx-RolesPersonalization>
                var card = new JobPreferenceCard(job, _sprite, _prototypeManager, _requirements, Profile, lockedReason, jobAlternatives); // <Onyx-RolesPersonalization-edited>
                card.OnPrioritySelected += selectedJobPrio => OnJobPrioritySelected(job, selectedJobPrio); // <Onyx-RolesPersonalization-edited>
                card.OnAlternativeSelected += alternativeId => OnJobAlternativeSelected(job, card, alternativeId); // <Onyx-RolesPersonalization-edited>
                _jobPriorities.Add((job.ID, card)); // <Onyx-RolesPersonalization-edited>
                _jobCards.Add((department.ID, card)); // <Onyx-RolesPersonalization>
                category.AddChild(card); // <Onyx-RolesPersonalization>
            }
        }

        UpdateJobPriorities();
        UpdateAlternativeJobs(); // <Onyx-AlternativeJobs>
        ApplyJobFilters(); // <Onyx-RolesPersonalization>
    }
    // </Onyx-RolesPersonalization-edited>

    public void RefreshAntags()
    {
        // <Onyx-CharacterPersonalizationFix>
        if (Profile == null)
            return;
        // </Onyx-CharacterPersonalizationFix>
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
                HorizontalExpand = true, // <Onyx-AntagPersonalization>
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
                // <Onyx-ProfilePersistence-edited>
                if (!_settingProfile)
                {
                    Profile = Profile?.WithAntagPreference(antag.ID, false);
                    SetDirty();
                }
                // </Onyx-ProfilePersistence-edited>
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

            selector.HorizontalExpand = true; // <Onyx-AntagPersonalization>
            antagContainer.AddChild(selector);

            AddAntagCard(antagContainer); // <Onyx-AntagPersonalization>
        }
    }
}
