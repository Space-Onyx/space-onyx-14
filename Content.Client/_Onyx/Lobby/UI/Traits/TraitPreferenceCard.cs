// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Client.Stylesheets;
using Content.Shared.Traits;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Onyx.Lobby.UI.Traits;

public sealed class TraitPreferenceCard : PanelContainer
{
    public readonly int Cost;

    public bool Preference
    {
        get => _checkbox.Pressed;
        set
        {
            _checkbox.Pressed = value;
            UpdateSelectedStyle();
        }
    }

    public event Action<bool>? PreferenceChanged;

    private readonly CheckBox _checkbox;
    private bool _updating;

    public TraitPreferenceCard(TraitPrototype trait)
    {
        Cost = trait.Cost;
        StyleClasses.Add("OnyxTraitCard");
        Margin = new Thickness(0, 0, 0, 4);
        HorizontalExpand = true;

        _checkbox = new CheckBox
        {
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        _checkbox.OnToggled += args =>
        {
            UpdateSelectedStyle();
            if (_updating)
                return;

            PreferenceChanged?.Invoke(args.Pressed);
        };

        var name = new Label
        {
            Text = Loc.GetString(trait.Name),
            StyleClasses = { StyleClass.LabelHeading },
            HorizontalExpand = true,
        };

        var cost = new Label
        {
            Text = Loc.GetString("trait-personalization-cost", ("cost", trait.Cost)),
            StyleClasses = { trait.Cost > 0 ? StyleClass.Negative : trait.Cost < 0 ? StyleClass.Positive : StyleClass.LabelWeak },
        };

        var title = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        title.AddChild(name);
        title.AddChild(cost);

        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8, 6),
            HorizontalExpand = true,
        };
        content.AddChild(title);

        if (trait.Description is { } description)
        {
            var descriptionLabel = new RichTextLabel
            {
                HorizontalExpand = true,
                StyleClasses = { StyleClass.LabelWeak },
                Margin = new Thickness(0, 3, 0, 0),
            };
            descriptionLabel.SetMessage(Loc.GetString(description));
            content.AddChild(descriptionLabel);
        }

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };
        row.AddChild(_checkbox);
        row.AddChild(content);
        AddChild(row);
        UpdateSelectedStyle();
    }

    private void UpdateSelectedStyle()
    {
        if (_checkbox.Pressed)
            AddStyleClass("OnyxTraitCardSelected");
        else
            RemoveStyleClass("OnyxTraitCardSelected");
    }

    public void SetPreferenceSilently(bool selected)
    {
        _updating = true;
        Preference = selected;
        _updating = false;
    }
}
