using System.Linq;
using Content.Client._Onyx.Chat;
using Robust.Client.Input;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

public sealed class EmoteKeyBindButton : Button
{
    private static readonly Color ColorRecording = Color.FromHex("#e0a030");

    private readonly IInputManager _input;
    private readonly List<int> _boundKeys = new();
    private readonly List<int> _captured = new();
    private readonly HashSet<int> _held = new();
    private bool _listening;

    public List<int> BoundKeys
    {
        get => new(_boundKeys);
        set
        {
            _boundKeys.Clear();
            _boundKeys.AddRange(value.Where(IsAllowedKey));
            StopListening();
            UpdateText();
        }
    }

    public EmoteKeyBindButton()
    {
        _input = IoCManager.Resolve<IInputManager>();
        HorizontalExpand = true;
        UpdateText();
        OnPressed += _ => StartListening();
    }

    public void StartListening()
    {
        if (_listening)
            return;

        _listening = true;
        _captured.Clear();
        _held.Clear();
        CustomEmotesSystem.SuppressHotkeys = true;
        Text = "...";
        ModulateSelfOverride = ColorRecording;
        _input.FirstChanceOnKeyEvent += OnKeyEvent;
    }

    private void OnKeyEvent(KeyEventArgs args, KeyEventType type)
    {
        var key = (int) args.Key;

        if (type == KeyEventType.Up)
        {
            _held.Remove(key);
            if (_listening && _held.Count == 0 && _captured.Count > 0)
            {
                args.Handle();
                _boundKeys.Clear();
                _boundKeys.AddRange(_captured);
                StopListening();
                UpdateText();
            }
            return;
        }

        if (type != KeyEventType.Down || args.IsRepeat)
            return;

        args.Handle();

        if (args.Key == Keyboard.Key.Escape)
        {
            StopListening();
            UpdateText();
            return;
        }

        if (!IsAllowedKey(key))
            return;

        if (!_captured.Contains(key))
            _captured.Add(key);
        _held.Add(key);
    }

    private static bool IsAllowedKey(int key)
    {
        return key != (int) Keyboard.Key.MouseLeft && key != (int) Keyboard.Key.MouseRight;
    }

    private void StopListening()
    {
        _listening = false;
        CustomEmotesSystem.SuppressHotkeys = false;
        _input.FirstChanceOnKeyEvent -= OnKeyEvent;
    }

    private void UpdateText()
    {
        ModulateSelfOverride = null;
        Text = _boundKeys.Count > 0
            ? Loc.GetString("hud-chatbox-emote-bind-value",
                ("key", string.Join(" + ", _boundKeys.Select(k => ((Keyboard.Key) k).ToString()))))
            : Loc.GetString("hud-chatbox-emote-bind-empty");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        StopListening();
    }
}
