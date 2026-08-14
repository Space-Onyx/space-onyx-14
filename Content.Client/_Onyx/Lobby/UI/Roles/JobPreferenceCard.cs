using System.Numerics;
using System.Linq;
using Content.Client._Onyx.AlternativeJobs;
using Content.Client.Players.PlayTimeTracking;
using Content.Shared._Onyx.AlternativeJobs;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Onyx.Lobby.UI.Roles;

public sealed class JobPreferenceCard : PanelContainer
{
    private readonly AlternativeJobSelector _jobSelector;
    private readonly TextureRect _icon;
    private readonly Label _roleName;
    private readonly JobCardOptionButton _priority = new();
    private readonly string[] _searchNames;
    private readonly StyleBoxFlat _availableStyle = Style("#1B1E24", "#3F4651");
    private readonly StyleBoxFlat _selectedStyle = Style("#182535", "#4C7DA8");
    private readonly StyleBoxFlat _lockedStyle = Style("#1C1A18", "#8A6434");
    private readonly bool _locked;

    public string JobId { get; }
    public JobPriority Priority => (JobPriority) _priority.SelectedId;
    public bool IsAvailable => !_locked;

    public event Action<JobPriority>? OnPrioritySelected;
    public event Action<ProtoId<AlternativeJobPrototype>?>? OnAlternativeSelected;

    public JobPreferenceCard(
        JobPrototype job,
        SpriteSystem sprites,
        IPrototypeManager prototypes,
        JobRequirementsManager requirements,
        HumanoidCharacterProfile? profile,
        FormattedMessage? lockedReason)
    {
        JobId = job.ID;
        _locked = lockedReason != null;
        MinWidth = 0;
        MinHeight = 176;
        HorizontalExpand = true;
        RectClipContent = true;
        PanelOverride = lockedReason == null ? _availableStyle : _lockedStyle;

        var alternatives = prototypes.EnumeratePrototypes<AlternativeJobPrototype>()
            .Where(alternative => alternative.ParentJobId == job.ID)
            .ToArray();
        _searchNames = alternatives.Select(alternative => alternative.LocalizedJobName)
            .Prepend(job.LocalizedName)
            .ToArray();

        _icon = new TextureRect
        {
            Texture = sprites.Frame0(prototypes.Index<JobIconPrototype>(job.Icon).Icon),
            SetSize = new Vector2(48),
            MinSize = new Vector2(48),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            VerticalAlignment = VAlignment.Top,
            MouseFilter = MouseFilterMode.Ignore,
            Modulate = lockedReason == null ? Color.White : Color.FromHex("#8B8275"),
        };

        _jobSelector = new AlternativeJobSelector(job.ID, sprites, requirements, profile, true)
        {
            HorizontalExpand = true,
            MinHeight = 34,
        };
        _jobSelector.SetSelectedIconVisible(false);
        _jobSelector.SetJobCardStyle(_locked);
        _roleName = new Label
        {
            Text = job.LocalizedName,
            StyleClasses = { "font-large", "font-bold" },
            FontColorOverride = Color.FromHex(_locked ? "#C7BBA7" : "#C7D5E0"),
            ClipText = true,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
        };
        _jobSelector.OnAlternativeSelected += alternative =>
        {
            _icon.Texture = _jobSelector.SelectedIcon;
            _roleName.Text = _jobSelector.SelectedName;
            OnAlternativeSelected?.Invoke(alternative);
        };

        AddPriorityItems();
        _priority.HorizontalExpand = true;
        _priority.MinHeight = 32;
        _priority.SetLocked(_locked);
        _priority.OnItemSelected += args =>
        {
            _priority.SelectId(args.Id);
            OnPrioritySelected?.Invoke((JobPriority) args.Id);
        };

        var roleNameRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            HorizontalExpand = true,
            Children = { _roleName },
        };

        if (_jobSelector.HasAlternatives)
        {
            _jobSelector.ToolTip = Loc.GetString("job-personalization-role-name-hint");
            _jobSelector.MinWidth = 40;
            _jobSelector.MaxWidth = 40;
            _jobSelector.HorizontalExpand = false;
            _jobSelector.SetCompactDisplay(_locked);
            roleNameRow.AddChild(_jobSelector);
        }
        else
            _jobSelector.Visible = false;

        var controls = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
            Children =
            {
                roleNameRow,
                new PanelContainer { StyleClasses = { "LowDivider" } },
                _priority,
            },
        };

        var description = new RichTextLabel
        {
            HorizontalExpand = true,
        };
        description.SetMessage(FormattedMessage.FromUnformatted(
            job.LocalizedDescription ?? Loc.GetString("job-personalization-no-description")));

        var descriptionContent = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(7, 5),
            SeparationOverride = 5,
            HorizontalExpand = true,
            Children = { description },
        };

        if (lockedReason != null)
        {
            descriptionContent.AddChild(new PanelContainer
            {
                PanelOverride = Style("#2A2118", "#8A6434"),
                HorizontalExpand = true,
                Children =
                {
                    new BoxContainer
                    {
                        Orientation = BoxContainer.LayoutOrientation.Horizontal,
                        Margin = new Thickness(6, 3),
                        SeparationOverride = 5,
                        Children =
                        {
                            new TextureRect
                            {
                                TexturePath = "/Textures/Interface/Nano/lock.svg.192dpi.png",
                                SetSize = new Vector2(14),
                                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                                Modulate = Color.FromHex("#F6BE68"),
                            },
                            new Label
                            {
                                Text = Loc.GetString("job-personalization-locked-hint"),
                                StyleClasses = { "font-small", "font-bold" },
                                FontColorOverride = Color.FromHex("#F6BE68"),
                                ClipText = true,
                                HorizontalExpand = true,
                            },
                        },
                    },
                },
            });
        }

        var descriptionPanel = new PanelContainer
        {
            PanelOverride = Style(lockedReason == null ? "#16191F" : "#1F1B17",
                lockedReason == null ? "#353B45" : "#624D35"),
            HorizontalExpand = true,
            Children = { descriptionContent },
        };

        var top = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
            Children =
            {
                _icon,
                controls,
            },
        };

        AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(9, 8),
            SeparationOverride = 7,
            HorizontalExpand = true,
            VerticalExpand = true,
            Children =
            {
                top,
                new PanelContainer { StyleClasses = { "LowDivider" } },
                descriptionPanel,
            },
        });

        if (lockedReason != null)
        {
            _jobSelector.Disabled = true;
            _priority.Disabled = true;
            AddChild(new Control
            {
                HorizontalExpand = true,
                VerticalExpand = true,
                MouseFilter = MouseFilterMode.Stop,
                TooltipSupplier = _ => CreateTooltip(job, lockedReason),
            });
        }
    }

    public bool Matches(string search)
    {
        return string.IsNullOrWhiteSpace(search) ||
               _searchNames.Any(name => name.Contains(search, StringComparison.CurrentCultureIgnoreCase));
    }

    public void SelectPriority(JobPriority priority)
    {
        _priority.SelectId((int) priority);
        if (!_locked)
            PanelOverride = priority == JobPriority.Never ? _availableStyle : _selectedStyle;
        _priority.SetAccent(priority != JobPriority.Never);
    }

    public void SelectAlternative(ProtoId<AlternativeJobPrototype>? alternative)
    {
        _jobSelector.SelectAlternative(alternative);
        _icon.Texture = _jobSelector.SelectedIcon;
        _roleName.Text = _jobSelector.SelectedName;
    }

    private void AddPriorityItems()
    {
        _priority.AddItem(Loc.GetString("humanoid-profile-editor-job-priority-never-button"), (int) JobPriority.Never);
        _priority.AddItem(Loc.GetString("humanoid-profile-editor-job-priority-low-button"), (int) JobPriority.Low);
        _priority.AddItem(Loc.GetString("humanoid-profile-editor-job-priority-medium-button"), (int) JobPriority.Medium);
        _priority.AddItem(Loc.GetString("humanoid-profile-editor-job-priority-high-button"), (int) JobPriority.High);
    }

    private static Tooltip CreateTooltip(JobPrototype job, FormattedMessage? lockedReason)
    {
        var tooltip = new Tooltip();
        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MinWidth = 280,
            MaxWidth = 380,
            Margin = new Thickness(8, 6),
            SeparationOverride = 5,
        };
        body.AddChild(new Label
        {
            Text = job.LocalizedName,
            StyleClasses = { "font-bold", "font-large" },
            FontColorOverride = Color.FromHex("#8BC5FF"),
        });

        var description = new RichTextLabel { HorizontalExpand = true };
        description.SetMessage(FormattedMessage.FromUnformatted(job.LocalizedDescription ?? string.Empty));
        body.AddChild(description);

        if (lockedReason != null)
        {
            body.AddChild(new Label
            {
                Text = Loc.GetString("job-personalization-locked-title"),
                StyleClasses = { "font-bold" },
                FontColorOverride = Color.FromHex("#F6BE68"),
            });
            var reason = new RichTextLabel { HorizontalExpand = true };
            reason.SetMessage(lockedReason);
            body.AddChild(reason);
        }

        tooltip.AddChild(body);
        return tooltip;
    }

    private static StyleBoxFlat Style(string background, string border) => new()
    {
        BackgroundColor = Color.FromHex(background),
        BorderColor = Color.FromHex(border),
        BorderThickness = new Thickness(1),
        ContentMarginLeftOverride = 2,
        ContentMarginTopOverride = 2,
        ContentMarginRightOverride = 2,
        ContentMarginBottomOverride = 2,
    };
}

public sealed class JobCardOptionButton : OptionButton
{
    private bool _locked;
    private bool _accent;

    public void SetLocked(bool locked)
    {
        _locked = locked;
        UpdateStyle();
    }

    public void SetAccent(bool accent)
    {
        _accent = accent;
        UpdateStyle();
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        UpdateStyle();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var measured = base.MeasureOverride(availableSize);
        return float.IsPositiveInfinity(availableSize.X)
            ? measured
            : new Vector2(Math.Min(measured.X, availableSize.X), measured.Y);
    }

    private void UpdateStyle()
    {
        StyleBoxOverride = JobCardStyles.Button(DrawMode, _locked, _accent);
    }
}

public static class JobCardStyles
{
    public static StyleBoxFlat Button(BaseButton.DrawModeEnum mode, bool locked, bool accent)
    {
        if (locked)
        {
            return mode == BaseButton.DrawModeEnum.Hover
                ? Style("#30271F", "#9A7442")
                : Style("#24201C", "#624D35");
        }

        return mode switch
        {
            BaseButton.DrawModeEnum.Pressed => Style("#203A52", "#79B5E8"),
            BaseButton.DrawModeEnum.Hover => Style("#223344", "#567D9F"),
            _ when accent => Style("#1B3044", "#4C7DA8"),
            _ => Style("#202832", "#3F5264"),
        };
    }

    private static StyleBoxFlat Style(string background, string border) => new()
    {
        BackgroundColor = Color.FromHex(background),
        BorderColor = Color.FromHex(border),
        BorderThickness = new Thickness(1),
        ContentMarginLeftOverride = 8,
        ContentMarginTopOverride = 5,
        ContentMarginRightOverride = 8,
        ContentMarginBottomOverride = 5,
    };
}
