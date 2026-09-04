using System.Linq;
using Content.Client._Onyx.Chat;
using Content.Shared._Onyx.Chat;
using Content.Shared.Chat.Prototypes;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

public sealed class CustomEmoteEditorPopup : Popup
{
    private const int TypeNormal = 0;
    private const int TypeCustom = 1;

    private readonly CustomEmotesSystem _emotes;
    private readonly IPrototypeManager _proto;
    private CustomEmoteEntry? _editing;

    private readonly OptionButton _typeSelect;
    private readonly BoxContainer _normalBox;
    private readonly BoxContainer _customBox;

    private readonly OptionButton _emoteSelect;
    private readonly List<string> _emoteIds = new();
    private readonly LineEdit _normalName;
    private readonly EmoteKeyBindButton _normalBind;

    private readonly LineEdit _customName;
    private readonly LineEdit _customText;
    private readonly OptionButton _perspective;
    private readonly OptionButton _range;
    private readonly BoxContainer _radiusControls;
    private readonly Slider _radius;
    private readonly Label _radiusValue;
    private readonly CheckBox _showToGhosts;
    private readonly OptionButton _sound;
    private readonly List<string?> _soundIds = new();
    private readonly EmoteKeyBindButton _customBind;

    public CustomEmoteEditorPopup()
    {
        _emotes = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<CustomEmotesSystem>();
        _proto = IoCManager.Resolve<IPrototypeManager>();

        _typeSelect = new OptionButton { HorizontalExpand = true };
        _typeSelect.AddItem(Loc.GetString("hud-chatbox-emote-editor-type-normal"), TypeNormal);
        _typeSelect.AddItem(Loc.GetString("hud-chatbox-emote-editor-type-custom"), TypeCustom);
        _typeSelect.SelectId(TypeNormal);

        _emoteSelect = new OptionButton { HorizontalExpand = true };
        foreach (var emote in _proto.EnumeratePrototypes<EmotePrototype>()
                     .Where(e => e.Category != EmoteCategory.Invalid && e.ChatTriggers.Count > 0)
                     .OrderBy(e => e.ID))
        {
            _emoteIds.Add(emote.ID);
            _emoteSelect.AddItem(GetEmoteDisplayName(emote), _emoteIds.Count - 1);
        }
        if (_emoteIds.Count > 0)
            _emoteSelect.SelectId(0);
        _emoteSelect.OnItemSelected += args => _emoteSelect.SelectId(args.Id);

        _normalName = new LineEdit
        {
            PlaceHolder = Loc.GetString("hud-chatbox-emote-editor-name-placeholder"),
            HorizontalExpand = true,
        };
        _normalBind = new EmoteKeyBindButton();
        _normalBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalExpand = true,
            Children =
            {
                CreateSection("hud-chatbox-emote-editor-emote", _emoteSelect),
                CreateSection("hud-chatbox-emote-editor-name", _normalName),
                CreateSection("hud-chatbox-emote-editor-bind", _normalBind),
            },
        };

        _customName = new LineEdit
        {
            PlaceHolder = Loc.GetString("hud-chatbox-emote-editor-name-placeholder"),
            HorizontalExpand = true,
        };
        _customText = new LineEdit
        {
            PlaceHolder = Loc.GetString("hud-chatbox-emote-editor-text-placeholder"),
            HorizontalExpand = true,
        };

        _perspective = new OptionButton { HorizontalExpand = true };
        _perspective.AddItem(Loc.GetString("hud-chatbox-emote-visibility-perspective-first-person"), (int) EmotePerspective.FirstPerson);
        _perspective.AddItem(Loc.GetString("hud-chatbox-emote-visibility-perspective-third-person"), (int) EmotePerspective.ThirdPerson);
        _perspective.SelectId((int) EmoteVisibilityOptions.Default.Perspective);
        _perspective.OnItemSelected += args => _perspective.SelectId(args.Id);

        _range = new OptionButton { HorizontalExpand = true };
        _range.AddItem(Loc.GetString("hud-chatbox-emote-visibility-range-radius"), (int) EmoteVisibilityRange.Radius);
        _range.AddItem(Loc.GetString("hud-chatbox-emote-visibility-range-surrounding"), (int) EmoteVisibilityRange.Surrounding);
        _range.SelectId((int) EmoteVisibilityOptions.Default.Range);

        _radiusValue = new Label();
        _radius = new Slider
        {
            MinValue = EmoteVisibilityOptions.MinRadius,
            MaxValue = EmoteVisibilityOptions.MaxRadius,
            Value = EmoteVisibilityOptions.Default.Radius,
            Rounded = true,
            HorizontalExpand = true,
        };
        _radius.OnValueChanged += _ => UpdateRadiusValue();
        _radiusControls = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
            Visible = EmoteVisibilityOptions.Default.Range == EmoteVisibilityRange.Radius,
            Children = { _radiusValue, _radius },
        };
        _range.OnItemSelected += args =>
        {
            _range.SelectId(args.Id);
            _radiusControls.Visible = args.Id == (int) EmoteVisibilityRange.Radius;
        };
        UpdateRadiusValue();

        _showToGhosts = new CheckBox
        {
            Text = Loc.GetString("hud-chatbox-emote-visibility-show-to-ghosts"),
            Pressed = EmoteVisibilityOptions.Default.ShowToGhosts,
        };

        _sound = new OptionButton { HorizontalExpand = true };
        _soundIds.Add(null);
        _sound.AddItem(Loc.GetString("hud-chatbox-emote-editor-sound-none"), 0);
        foreach (var sound in _proto.EnumeratePrototypes<CustomEmoteSoundPrototype>().OrderBy(s => s.Name))
        {
            _soundIds.Add(sound.ID);
            _sound.AddItem(sound.Name, _soundIds.Count - 1);
        }
        _sound.SelectId(0);
        _sound.OnItemSelected += args => _sound.SelectId(args.Id);

        _customBind = new EmoteKeyBindButton();
        _customBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalExpand = true,
            Visible = false,
            Children =
            {
                CreateSection("hud-chatbox-emote-editor-name", _customName),
                CreateSection("hud-chatbox-emote-editor-text", _customText),
                CreateSection("hud-chatbox-emote-visibility-perspective", _perspective),
                CreateSection("hud-chatbox-emote-visibility-range", _range, _radiusControls),
                CreateSection("hud-chatbox-emote-visibility-observers", _showToGhosts),
                CreateSection("hud-chatbox-emote-editor-sound", _sound),
                CreateSection("hud-chatbox-emote-editor-bind", _customBind),
            },
        };
        _typeSelect.OnItemSelected += args =>
        {
            _typeSelect.SelectId(args.Id);
            _normalBox.Visible = args.Id == TypeNormal;
            _customBox.Visible = args.Id == TypeCustom;
        };

        var save = new Button
        {
            Text = Loc.GetString("hud-chatbox-emote-editor-save"),
            HorizontalAlignment = HAlignment.Center,
        };
        save.OnPressed += _ => Save();

        AddChild(new PanelContainer
        {
            StyleClasses = { "BorderedWindowPanel" },
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    SeparationOverride = 6,
                    Margin = new Thickness(10),
                    MinWidth = 340,
                    Children =
                    {
                        new Label
                        {
                            Text = Loc.GetString("hud-chatbox-emote-editor-title"),
                            StyleClasses = { "LabelHeading" },
                        },
                        _typeSelect,
                        _normalBox,
                        _customBox,
                        save,
                    },
                },
            },
        });
    }

    private void Save()
    {
        if (_editing != null)
        {
            if (!FillEntry(_editing))
                return;

            _emotes.SaveChanges();
            Close();
            return;
        }

        var entry = new CustomEmoteEntry();
        if (!FillEntry(entry))
            return;

        _emotes.Add(entry);
        Close();
    }

    public void Setup(CustomEmoteEntry? entry)
    {
        _editing = entry;
        if (entry == null)
            return;

        if (entry.Custom)
        {
            _typeSelect.SelectId(TypeCustom);
            _normalBox.Visible = false;
            _customBox.Visible = true;
            _customName.Text = entry.Name;
            _customText.Text = entry.Text ?? string.Empty;
            _perspective.SelectId((int) entry.Perspective);
            _range.SelectId((int) entry.Range);
            _radiusControls.Visible = entry.Range == EmoteVisibilityRange.Radius;
            _radius.Value = Math.Clamp(entry.Radius, EmoteVisibilityOptions.MinRadius, EmoteVisibilityOptions.MaxRadius);
            UpdateRadiusValue();
            _showToGhosts.Pressed = entry.ShowToGhosts;
            _sound.SelectId(Math.Max(0, _soundIds.IndexOf(entry.SoundId)));
            _customBind.BoundKeys = entry.BindKeys;
        }
        else
        {
            _typeSelect.SelectId(TypeNormal);
            _normalBox.Visible = true;
            _customBox.Visible = false;
            _emoteSelect.SelectId(Math.Max(0, _emoteIds.IndexOf(entry.EmoteId ?? string.Empty)));
            _normalName.Text = entry.Name;
            _normalBind.BoundKeys = entry.BindKeys;
        }
    }

    private bool FillEntry(CustomEmoteEntry entry)
    {
        if (_typeSelect.SelectedId == TypeNormal)
        {
            if (_emoteIds.Count == 0 || _emoteSelect.SelectedId < 0 || _emoteSelect.SelectedId >= _emoteIds.Count)
                return false;

            var emoteId = _emoteIds[_emoteSelect.SelectedId];
            var name = _normalName.Text.Trim();
            if (name.Length == 0 && _proto.TryIndex<EmotePrototype>(emoteId, out var proto))
                name = GetEmoteDisplayName(proto);
            if (name.Length == 0)
                return false;

            entry.Name = name;
            entry.Custom = false;
            entry.EmoteId = emoteId;
            entry.BindKeys = _normalBind.BoundKeys;
            return true;
        }

        {
            var name = _customName.Text.Trim();
            var text = _customText.Text.Trim();
            if (name.Length == 0 || text.Length == 0)
                return false;

            entry.Name = name;
            entry.Custom = true;
            entry.Text = text;
            entry.Perspective = (EmotePerspective) _perspective.SelectedId;
            entry.Range = (EmoteVisibilityRange) _range.SelectedId;
            entry.Radius = (int) _radius.Value;
            entry.ShowToGhosts = _showToGhosts.Pressed;
            entry.SoundId = _sound.SelectedId >= 0 && _sound.SelectedId < _soundIds.Count
                ? _soundIds[_sound.SelectedId]
                : null;
            entry.BindKeys = _customBind.BoundKeys;
            return true;
        }
    }

    private void UpdateRadiusValue()
    {
        _radiusValue.Text = Loc.GetString("hud-chatbox-emote-visibility-radius-value", ("radius", (int) _radius.Value));
    }

    private static string GetEmoteDisplayName(EmotePrototype emote)
    {
        return Loc.TryGetString(emote.Name, out var name) ? name : emote.ID;
    }

    private static PanelContainer CreateSection(string title, params Control[] controls)
    {
        var contents = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Margin = new Thickness(6),
            HorizontalExpand = true,
            Children =
            {
                new Label
                {
                    Text = Loc.GetString(title),
                    StyleClasses = { "LabelSubText" },
                },
            },
        };

        foreach (var control in controls)
            contents.AddChild(control);

        return new PanelContainer
        {
            StyleClasses = { "BackgroundPanel" },
            Children = { contents },
        };
    }
}
