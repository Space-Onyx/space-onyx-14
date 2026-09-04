// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

#pragma warning disable IDE0130
namespace Content.Client.UserInterface.Controls;

public sealed partial class SlotButton
{
    private const int SubSlotButtonSize = 52;
    private const int SubSlotIndicatorSize = 12;
    private const int SubSlotSeparation = 2;

    private SubSlotPopup? _subSlots;
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
        button.SetButtonSize(SubSlotButtonSize);
        _subSlots!.AddButton(button);
        return true;
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
        public Vector2 PopupSize => new(
            _buttons.ChildCount * SubSlotButtonSize + Math.Max(0, _buttons.ChildCount - 1) * SubSlotSeparation,
            SubSlotButtonSize);

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

        public event Action? PointerExited;

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
        }
        return true;
    }
}
