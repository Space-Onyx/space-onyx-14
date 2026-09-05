// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Client.UserInterface.Controls;
using Content.Shared._Onyx.ActiveAction;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Onyx.ActiveAction;

public sealed class ActiveActionWindow : FancyWindow
{
    private readonly LineEdit _input;

    public event Action<string>? Submitted;

    public ActiveActionWindow(string text)
    {
        Title = Loc.GetString("active-action-window-title");
        MinWidth = 360;

        _input = new LineEdit
        {
            Text = text,
            PlaceHolder = Loc.GetString("active-action-window-placeholder"),
            HorizontalExpand = true,
            IsValid = value => value.Length <= SharedActiveActionSystem.MaxLength,
        };
        _input.OnTextEntered += args => Submit(args.Text);

        var confirm = new Button { Text = Loc.GetString("active-action-window-confirm") };
        confirm.OnPressed += _ => Submit(_input.Text);

        var clear = new Button { Text = Loc.GetString("active-action-window-clear") };
        clear.OnPressed += _ => Submit(string.Empty);

        ContentsContainer.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(8),
            Children =
            {
                new Label { Text = Loc.GetString("active-action-window-description") },
                _input,
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    SeparationOverride = 8,
                    HorizontalAlignment = HAlignment.Right,
                    Children = { clear, confirm },
                },
            },
        });
    }

    protected override void Opened()
    {
        base.Opened();
        _input.GrabKeyboardFocus();
    }

    private void Submit(string text)
    {
        Submitted?.Invoke(text);
        Close();
    }
}
