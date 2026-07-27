using Content.Client.Stylesheets.Palette;
using Content.Client.UserInterface.Controls;
using Content.Shared.Mech.Components;
using Content.Shared._Onyx.Mech;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client.Mech.Port;

public sealed class MechEquipmentSelectorBoundUserInterface(EntityUid owner, Enum uiKey)
    : BoundUserInterface(owner, uiKey)
{
    private static readonly Color SelectedColor = Palettes.Green.Element.WithAlpha(128);
    private static readonly Color SelectedHoverColor = Palettes.Green.HoveredElement.WithAlpha(128);

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent<MechComponent>(Owner, out var mech))
            return;

        var menu = this.CreateWindow<SimpleRadialMenu>();
        var options = new List<RadialMenuOptionBase>();
        foreach (var equipment in mech.EquipmentContainer.ContainedEntities)
        {
            var selected = mech.CurrentSelectedEquipment == equipment;
            options.Add(new RadialMenuActionOption<EntityUid>(selectedEquipment => SelectEquipment(selectedEquipment), equipment)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(equipment),
                ToolTip = EntMan.GetComponent<MetaDataComponent>(equipment).EntityName,
                BackgroundColor = selected ? SelectedColor : null,
                HoverBackgroundColor = selected ? SelectedHoverColor : null,
            });
        }

        options.Add(new RadialMenuActionOption<EntityUid?>(SelectEquipment, null)
        {
            IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Texture(
                new("/Textures/Interface/VerbIcons/delete.svg.192dpi.png"))),
            ToolTip = Loc.GetString("mech-equipment-select-none-popup"),
            BackgroundColor = mech.CurrentSelectedEquipment == null ? SelectedColor : null,
            HoverBackgroundColor = mech.CurrentSelectedEquipment == null ? SelectedHoverColor : null,
        });

        menu.SetButtons(options);
        menu.OpenOverMouseScreenPosition();
    }

    private void SelectEquipment(EntityUid? equipment)
    {
        SendPredictedMessage(new MechEquipmentSelectMessage(EntMan.GetNetEntity(equipment)));
    }
}
