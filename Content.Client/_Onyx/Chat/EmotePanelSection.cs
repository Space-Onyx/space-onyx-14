using System.Numerics;
using Content.Client._Onyx.Chat;
using Content.Client.Resources;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

public sealed class EmotePanelSection : PanelContainer
{
    private static readonly Color AddButtonColor = Color.FromHex("#90ee90");
    private const int MaxListHeight = 96;

    private CustomEmotesSystem? _emotes;
    private readonly BoxContainer _list;
    private readonly ScrollContainer _scroll;
    private readonly List<BoxContainer> _cells = new();
    private readonly Dictionary<BoxContainer, float> _cellWidths = new();
    private float _lastListWidth;

    public EmotePanelSection()
    {
        var addButton = new Button
        {
            ToolTip = Loc.GetString("hud-chatbox-emote-panel-add-tooltip"),
            StyleClasses = { ChatInputBox.StyleClassChatFilterOptionButton },
            VerticalAlignment = VAlignment.Top,
        };
        addButton.AddChild(new TextureRect
        {
            Texture = IoCManager.Resolve<IResourceCache>()
                .GetTexture("/Textures/_Onyx/Interface/Chat/plus.png"),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            ModulateSelfOverride = AddButtonColor,
            MouseFilter = MouseFilterMode.Ignore,
        });
        addButton.OnPressed += _ => OpenEditor();

        _list = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
            HorizontalExpand = true,
        };
        _scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            HScrollEnabled = false,
            ReturnMeasure = true,
            MinHeight = 22,
            MaxHeight = MaxListHeight,
            Children = { _list },
        };

        AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 2,
            Margin = new Thickness(8, 2, 8, 2),
            HorizontalExpand = true,
            Children =
            {
                _scroll,
                addButton,
            },
        });
    }

    public void EnsureInitialized()
    {
        if (_emotes != null)
            return;

        _emotes = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<CustomEmotesSystem>();
        _emotes.EntriesChanged += Refresh;
        Refresh();
    }

    private void OpenEditor(CustomEmoteEntry? entry = null)
    {
        var editor = UserInterfaceManager.CreatePopup<CustomEmoteEditorPopup>();
        editor.Setup(entry);
        var size = new Vector2(360, editor.MinSize.Y);
        editor.Open(UIBox2.FromDimensions(GlobalPosition, size));
    }

    private void Refresh()
    {
        if (_emotes == null)
            return;

        foreach (var cell in _cells)
            cell.Orphan();

        _cells.Clear();
        _cellWidths.Clear();
        _list.RemoveAllChildren();

        if (_emotes.Entries.Count == 0)
        {
            var empty = new RichTextLabel { HorizontalExpand = true };
            var message = new FormattedMessage();
            message.AddText(Loc.GetString("hud-chatbox-emote-panel-empty"));
            empty.SetMessage(message);
            _list.AddChild(empty);
            return;
        }

        foreach (var entry in _emotes.Entries)
        {
            var captured = entry;
            var play = new Button
            {
                Text = captured.Name,
                HorizontalExpand = true,
                MinSize = new Vector2(0, 22),
                StyleClasses = { "OpenRight" },
            };
            play.SizeFlagsStretchRatio = 1f;
            play.OnPressed += _ => _emotes?.Play(captured);
            var delete = new Button
            {
                Text = "X",
                ToolTip = Loc.GetString("hud-chatbox-emote-panel-delete-tooltip"),
                StyleClasses = { "OpenLeft" },
            };
            delete.OnPressed += _ => _emotes?.Remove(captured);
            var settings = new Button
            {
                Text = "⚙",
                ToolTip = Loc.GetString("hud-chatbox-emote-panel-settings-tooltip"),
                StyleClasses = { "OpenBoth" },
            };
            settings.OnPressed += _ => OpenEditor(captured);

            var cell = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 0,
            };
            cell.AddChild(play);
            cell.AddChild(settings);
            cell.AddChild(delete);
            cell.Measure(Vector2Helpers.Infinity);
            _cellWidths[cell] = cell.DesiredSize.X;
            play.ClipText = true;
            _cells.Add(cell);
        }

        RebuildRows();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_cells.Count == 0 || MathF.Abs(_lastListWidth - _scroll.Width) < 1f)
            return;

        RebuildRows();
    }

    private void RebuildRows()
    {
        foreach (var cell in _cells)
            cell.Orphan();

        _list.RemoveAllChildren();
        _lastListWidth = _scroll.Width;
        var availableWidth = _lastListWidth;
        if (availableWidth <= 0)
            return;

        BoxContainer? row = null;
        var usedWidth = 0f;

        foreach (var cell in _cells)
        {
            var naturalWidth = _cellWidths[cell];
            var width = Math.Min(naturalWidth, availableWidth);
            if (row == null || usedWidth + 2f + width > availableWidth)
            {
                row = new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    SeparationOverride = 2,
                    HorizontalExpand = true,
                };
                _list.AddChild(row);
                usedWidth = 0;
            }

            cell.SetWidth = width;
            row.AddChild(cell);
            usedWidth += (usedWidth > 0 ? 2f : 0f) + width;

            if (cell.GetChild(0) is Button play)
                play.ToolTip = naturalWidth > availableWidth ? play.Text : null;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        if (_emotes != null)
            _emotes.EntriesChanged -= Refresh;
    }
}
