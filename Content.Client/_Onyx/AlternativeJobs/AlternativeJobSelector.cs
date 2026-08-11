using System.Linq;
using Content.Client._Onyx.Lobby.UI.Loadouts;
using Content.Client.Players.PlayTimeTracking;
using Content.Shared._Onyx.AlternativeJobs;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Onyx.AlternativeJobs;

public sealed partial class AlternativeJobSelector : LoadoutRoleSelector
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    private readonly List<AlternativeJobPrototype> _alternatives = [];
    private readonly HashSet<ProtoId<AlternativeJobPrototype>> _lockedAlternatives = [];
    private readonly SpriteSystem _spriteSystem;
    private readonly ProtoId<JobIconPrototype> _parentJobIcon;
    private FormattedMessage? _addingRequirement;

    public event Action<ProtoId<AlternativeJobPrototype>?>? OnAlternativeSelected;

    public AlternativeJobSelector(
        ProtoId<JobPrototype> parentJobId,
        SpriteSystem spriteSystem,
        JobRequirementsManager requirements,
        HumanoidCharacterProfile? profile)
    {
        IoCManager.InjectDependencies(this);
        _spriteSystem = spriteSystem;

        if (_prototypeManager.TryIndex(parentJobId, out var job))
        {
            _parentJobIcon = job.Icon;
            AddJob(job.LocalizedName, GetIcon(job.Icon), 0);
        }
        else
        {
            _parentJobIcon = "JobIconUnknown";
            AddItem(parentJobId, 0);
        }

        _alternatives.AddRange(_prototypeManager.EnumeratePrototypes<AlternativeJobPrototype>()
            .Where(alternative => alternative.ParentJobId == parentJobId)
            .OrderBy(alternative => alternative.LocalizedJobName));

        for (var i = 0; i < _alternatives.Count; i++)
        {
            if (!requirements.AreRequirementsMet(_alternatives[i].Requirements, profile, out var reason))
            {
                _lockedAlternatives.Add(_alternatives[i].ID);
                _addingRequirement = FormattedMessage.FromMarkupPermissive(
                    $"{Loc.GetString("alternative-job-requirement-locked")}\n{reason.ToMarkup()}");
            }

            AddJob(_alternatives[i].LocalizedJobName, GetIcon(_alternatives[i].JobIconProtoId ?? _parentJobIcon), i + 1);
            if (_addingRequirement != null)
                SetItemDisabled(i + 1, true);
            _addingRequirement = null;
        }

        Visible = _alternatives.Count > 0;
        OnItemSelected += args =>
        {
            SelectId(args.Id);
            SetSelectedIcon(GetSelectedIcon(args.Id));
            OnAlternativeSelected?.Invoke(args.Id == 0 ? null : _alternatives[args.Id - 1].ID);
        };

        SetSelectedIcon(GetSelectedIcon(0));
    }

    public void SelectAlternative(ProtoId<AlternativeJobPrototype>? alternativeId)
    {
        if (alternativeId is { } id && _lockedAlternatives.Contains(id))
            alternativeId = null;

        var index = alternativeId is null ? -1 : _alternatives.FindIndex(alternative => alternative.ID == alternativeId);
        SelectId(index + 1);
        SetSelectedIcon(GetSelectedIcon(index + 1));
    }

    private Texture GetIcon(ProtoId<JobIconPrototype> iconId)
    {
        return _spriteSystem.Frame0(_prototypeManager.Index(iconId).Icon);
    }

    private Texture GetSelectedIcon(int id)
    {
        var iconId = id == 0
            ? _parentJobIcon
            : _alternatives[id - 1].JobIconProtoId ?? _parentJobIcon;
        return GetIcon(iconId);
    }

    public override void ButtonOverride(Button button)
    {
        base.ButtonOverride(button);
        if (_addingRequirement == null)
            return;

        var tooltip = new Tooltip();
        tooltip.SetMessage(_addingRequirement);
        button.TooltipSupplier = _ => tooltip;
    }
}
