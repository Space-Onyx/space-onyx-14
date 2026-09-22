// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Client._Onyx.Lobby.UI.Roles;
using Content.Shared._Onyx.AlternativeJobs;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private readonly List<(string Department, JobPreferenceCard Card)> _jobCards = new();
    private readonly List<DepartmentPrototype> _jobFilterDepartments = new();
    private string _jobSearch = string.Empty;
    private string? _jobDepartmentFilter;
    private bool _showSelectedJobs;

    private void UpdateAlternativeJobs()
    {
        if (Profile is null)
            return;

        foreach (var (jobId, card) in _jobPriorities)
            card.SelectAlternative(Profile.JobAlternatives.GetValueOrDefault(jobId));
    }

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
        var visibleDepartments = new HashSet<string>();
        foreach (var (department, card) in _jobCards)
        {
            card.Visible = (_jobDepartmentFilter == null || department == _jobDepartmentFilter) &&
                           (!_showSelectedJobs || card.IsAvailable && card.Priority != JobPriority.Never) &&
                           card.Matches(_jobSearch);

            if (card.Visible)
                visibleDepartments.Add(department);
        }

        foreach (var (department, category) in _jobCategories)
            category.Visible = visibleDepartments.Contains(department);
    }

    private Dictionary<string, List<AlternativeJobPrototype>> GroupAlternativesByJob()
    {
        var alternativesByJob = new Dictionary<string, List<AlternativeJobPrototype>>();
        foreach (var alternative in _prototypeManager.EnumeratePrototypes<AlternativeJobPrototype>())
        {
            var parentJob = alternative.ParentJobId.Id;
            if (!alternativesByJob.TryGetValue(parentJob, out var list))
                alternativesByJob[parentJob] = list = new();
            list.Add(alternative);
        }
        return alternativesByJob;
    }

    private void RefreshJobDepartmentFilter(List<DepartmentPrototype> departments)
    {
        _jobFilterDepartments.Clear();
        _jobFilterDepartments.AddRange(departments);
        var selectedDepartment = _jobDepartmentFilter;
        JobDepartmentFilter.Clear();
        JobDepartmentFilter.AddItem(Loc.GetString("job-personalization-all-departments"), 0);
        for (var i = 0; i < departments.Count; i++)
            JobDepartmentFilter.AddItem(Loc.GetString(departments[i].Name), i + 1);
        var selectedDepartmentIndex = departments.FindIndex(department => department.ID == selectedDepartment) + 1;
        JobDepartmentFilter.SelectId(selectedDepartmentIndex);
        _jobDepartmentFilter = selectedDepartmentIndex == 0 ? null : selectedDepartment;
    }

    private void OnJobPrioritySelected(JobPrototype job, JobPriority selectedJobPrio)
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
        RefreshLoadoutPersonalization();

        UpdateJobPriorities();
        ApplyJobFilters();
        SetDirty();
    }

    private void OnJobAlternativeSelected(
        JobPrototype job,
        JobPreferenceCard card,
        ProtoId<AlternativeJobPrototype>? alternativeId)
    {
        Profile = Profile?.WithJobAlternative(job.ID, alternativeId);
        foreach (var (jobId, other) in _jobPriorities)
        {
            if (jobId == job.ID && other != card)
                other.SelectAlternative(alternativeId);
        }
        SetDirty();
    }
}
