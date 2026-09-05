// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

#pragma warning disable IDE0130
namespace Content.Client.UserInterface.Controls;

public sealed partial class SlotButton
{
    private const int SubSlotIndicatorSize = 12;
    private const int SubSlotSeparation = 2;

    private SubSlotPopup? _subSlots;
    private Label? _subSlotIndicator;
    private readonly Dictionary<string, SlotButton> _subSlotButtons = new();

    private void InitializeSubSlots()
    {
        RegisterSubSlotHover(ButtonRect);
        RegisterSubSlotHover(StorageButton);
        RegisterSubSlotHover(BlockedRect);
    }

    private void RegisterSubSlotHover(Control control)
    {
        control.OnMouseEntered += _ => OpenSubSlots();
        control.OnMouseExited += _ => CloseSubSlotsDeferred();
    }

    public bool TryAddSubSlot(SlotButton button)
    {
        if (!_subSlotButtons.TryAdd(button.SlotName, button))
            return false;

        EnsureSubSlotPopup();
        EnsureSubSlotIndicator();
        _subSlotIndicator!.Text = $"+{_subSlotButtons.Count}";
        button.SetButtonSize(DefaultButtonSize);
        _subSlots!.AddButton(button);
        return true;
    }

    private void EnsureSubSlotIndicator()
    {
        if (_subSlotIndicator != null)
            return;

        _subSlotIndicator = new Label
        {
            HorizontalAlignment = HAlignment.Left,
            VerticalAlignment = VAlignment.Top,
            MinSize = new Vector2(SubSlotIndicatorSize),
            Margin = new Thickness(6, 2, 0, 0),
            MouseFilter = MouseFilterMode.Ignore,
            Align = Label.AlignMode.Center,
            FontColorOverride = Color.Gray,
        };
        _subSlotIndicator.AddStyleClass(StyleClass.FontSmall);
        AddChild(_subSlotIndicator);
    }

    private void OpenSubSlots()
    {
        if (_subSlots == null || _subSlots.Visible)
            return;

        var popupSize = _subSlots.PopupSize;
        var rightPosition = new Vector2(GlobalPosition.X + Width, GlobalPosition.Y);
        var leftPosition = new Vector2(GlobalPosition.X - popupSize.X, GlobalPosition.Y);
        _subSlots.Open(UIBox2.FromDimensions(rightPosition, popupSize), leftPosition);
    }

    private void CloseSubSlotsDeferred()
    {
        Timer.Spawn(0, () =>
        {
            var mousePosition = UserInterfaceManager.MousePositionScaled.Position;
            var overParent = UIBox2.FromDimensions(GlobalPosition, Size).Contains(mousePosition);
            var overPopup = _subSlots is { Visible: true } &&
                            UIBox2.FromDimensions(_subSlots.GlobalPosition, _subSlots.Size).Contains(mousePosition);
            if (!overParent && !overPopup)
                _subSlots?.Close();
        });
    }

    private void EnsureSubSlotPopup()
    {
        if (_subSlots != null)
            return;

        _subSlots = new SubSlotPopup();
        UserInterfaceManager.ModalRoot.AddChild(_subSlots);
        _subSlots.PointerExited += CloseSubSlotsDeferred;
    }

    private sealed class SubSlotPopup : Popup
    {
        private readonly HashSet<Control> _visibleWindows = new();

        public Vector2 PopupSize => new(
            _buttons.ChildCount * DefaultButtonSize + Math.Max(0, _buttons.ChildCount - 1) * SubSlotSeparation,
            DefaultButtonSize);

        private readonly BoxContainer _buttons = new()
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = SubSlotSeparation,
        };

        public SubSlotPopup()
        {
            AddChild(_buttons);
        }

        public void AddButton(SlotButton button)
        {
            _buttons.AddChild(button);
        }

        public override void Open(UIBox2? box = null, Vector2? altPos = null, Vector2? altPosUp = null)
        {
            _visibleWindows.Clear();
            foreach (var window in UserInterfaceManager.WindowRoot.Children)
            {
                if (window.Visible)
                    _visibleWindows.Add(window);
            }

            base.Open(box, altPos, altPosUp);
        }

        public event Action? PointerExited;

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            if (!Visible)
                return;

            foreach (var window in UserInterfaceManager.WindowRoot.Children)
            {
                if (window.Visible && !_visibleWindows.Contains(window))
                {
                    Close();
                    return;
                }
            }
        }

        protected override void MouseExited()
        {
            base.MouseExited();
            PointerExited?.Invoke();
        }
    }

    public bool TryGetSubSlot(string slotName, [NotNullWhen(true)] out SlotButton? button) =>
        _subSlotButtons.TryGetValue(slotName, out button);

    public void DisposeSubSlots()
    {
        _subSlots?.Close();
        _subSlots?.Orphan();
        _subSlots = null;
    }

    public bool RemoveSubSlot(string slotName)
    {
        if (!_subSlotButtons.Remove(slotName, out var button))
            return false;

        button.Orphan();
        if (_subSlotButtons.Count == 0)
        {
            _subSlots?.Close();
            _subSlotIndicator?.Orphan();
            _subSlotIndicator = null;
        }
        else
            _subSlotIndicator!.Text = $"+{_subSlotButtons.Count}";
        return true;
    }
}
