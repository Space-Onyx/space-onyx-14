using System.Numerics;
using System.Linq;
using Content.Client._Onyx.Loadouts;
using Content.Shared.Clothing;
using Content.Shared.Preferences.Loadouts;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Onyx.Lobby.UI.Loadouts;

public sealed partial class LoadoutIconButton : Button
{
    [Dependency] private IEntityManager _entManager = default!;
    private readonly EntityUid? _entity;
    private readonly TextureRect? _lockIcon;

    public event Action<string, string>? OnCustomizePressed;

    public LoadoutIconButton(LoadoutPrototype loadout, string name, string? tint, FormattedMessage? reason)
    {
        IoCManager.InjectDependencies(this);
        ToggleMode = true;
        MinSize = SetSize = new Vector2(108, 132);
        StyleBoxOverride = Style("#2a2a35", "#32323e");

        var displayName = name;
        var description = string.Empty;
        var entity = ResolveDisplayEntity(loadout);
        var sprite = new SpriteView { Scale = new Vector2(3), OverrideDirection = Direction.South, SetSize = new Vector2(96), HorizontalAlignment = HAlignment.Center };
        if (entity != null)
        {
            _entity = _entManager.SpawnEntity(entity, MapCoordinates.Nullspace);
            SetCustomColor(tint);
            sprite.SetEntity(_entity);
            if (_entManager.TryGetComponent(_entity.Value, out MetaDataComponent? metadata))
            {
                description = metadata.EntityDescription;
            }
        }

        var caption = displayName.Length > 13 ? string.Concat(displayName.AsSpan(0, 12), "...") : displayName;
        AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            Children = { new Control { MinSize = new Vector2(0, 96), RectClipContent = true, Children = { sprite } }, new Label { Text = caption, ClipText = true, Align = Label.AlignMode.Center, HorizontalExpand = true, MinSize = new Vector2(0, 26), StyleClasses = { "font-small" } } },
        });

        _lockIcon = new TextureRect { TexturePath = "/Textures/Interface/Nano/lock.svg.192dpi.png", SetSize = new Vector2(16), HorizontalAlignment = HAlignment.Right, VerticalAlignment = VAlignment.Top, Stretch = TextureRect.StretchMode.KeepAspectCentered, Visible = false };
        AddChild(_lockIcon);
        TooltipSupplier = _ =>
        {
            return CreateTooltip(displayName, description, reason);
        };

        var customize = new ContainerButton
        {
            StyleBoxOverride = new StyleBoxEmpty(),
            MinSize = SetSize = new Vector2(24),
            HorizontalAlignment = HAlignment.Left,
            VerticalAlignment = VAlignment.Top,
            HorizontalExpand = false,
            VerticalExpand = false,
            ToolTip = Loc.GetString("loadout-customize"),
        };
        customize.AddChild(new TextureRect { TexturePath = "/Textures/Interface/Nano/gear.svg.192dpi.png", SetSize = new Vector2(16), HorizontalAlignment = HAlignment.Center, VerticalAlignment = VAlignment.Center, Stretch = TextureRect.StretchMode.KeepAspectCentered });
        customize.OnPressed += _ => OnCustomizePressed?.Invoke(displayName, description);
        AddChild(customize);
    }

    public void SetCustomColor(string? tint)
    {
        if (_entity != null && !string.IsNullOrEmpty(tint) && Color.TryFromHex(tint, out var color))
            _entManager.System<LoadoutTintSystem>().SetTint(_entity.Value, color);
    }

    private EntProtoId? ResolveDisplayEntity(LoadoutPrototype loadout)
    {
        if (loadout.DummyEntity != null)
            return loadout.DummyEntity;
        var entity = _entManager.System<LoadoutSystem>().GetFirstOrNull(loadout);
        if (entity != null)
            return entity;
        foreach (var equipment in loadout.Equipment.Values)
            return equipment;

        if (loadout.Inhand.Count > 0)
            return loadout.Inhand[0];

        foreach (var storage in loadout.Storage.Values)
        {
            if (storage.Count > 0)
                return storage[0];
        }

        return null;
    }

    private static Tooltip CreateTooltip(string name, string description, FormattedMessage? reason)
    {
        var tooltip = new Tooltip();
        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MinWidth = 260,
            MaxWidth = 360,
            Margin = new Thickness(8, 6),
        };
        body.AddChild(new Label
        {
            Text = name,
            FontColorOverride = Color.FromHex("#8bc5ff"),
            StyleClasses = { "font-bold", "font-large" },
            HorizontalExpand = true,
        });

        if (!string.IsNullOrWhiteSpace(description))
        {
            var descriptionLabel = new RichTextLabel { HorizontalExpand = true };
            descriptionLabel.SetMessage(FormattedMessage.FromUnformatted(description));
            body.AddChild(new PanelContainer
            {
                PanelOverride = Style("#18212d", "#3a526d"),
                Margin = new Thickness(0, 5, 0, 4),
                Children = { descriptionLabel },
            });
        }

        var available = reason == null;
        var status = new Label
        {
            Text = available ? Loc.GetString("loadout-tooltip-available") : Loc.GetString("loadout-tooltip-locked"),
            FontColorOverride = available ? Color.FromHex("#79d69b") : Color.FromHex("#f6be68"),
            StyleClasses = { "font-small", "font-bold" },
        };
        body.AddChild(status);
        if (reason != null)
        {
            var reasonLabel = new RichTextLabel { HorizontalExpand = true, StyleClasses = { "font-small" } };
            reasonLabel.SetMessage(reason);
            body.AddChild(reasonLabel);
        }

        tooltip.AddChild(body);
        return tooltip;
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        StyleBoxOverride = DrawMode switch
        {
            DrawModeEnum.Disabled => Style("#1a1a22", "#2a2a2a"),
            DrawModeEnum.Pressed => Style("#2a3a4a", "#60a5fa"),
            DrawModeEnum.Hover => Style("#2a3a4a", "#32323e"),
            _ => Style("#2a2a35", "#32323e"),
        };
        if (_lockIcon != null)
            _lockIcon.Visible = DrawMode == DrawModeEnum.Disabled;
    }

    private static StyleBoxFlat Style(string background, string border) => new()
    {
        BackgroundColor = Color.FromHex(background), BorderColor = Color.FromHex(border), BorderThickness = new Thickness(1), ContentMarginLeftOverride = 4, ContentMarginRightOverride = 4, ContentMarginTopOverride = 4, ContentMarginBottomOverride = 4,
    };

    [Obsolete]
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && _entity != null)
            _entManager.DeleteEntity(_entity);
    }
}
