using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client._Onyx.Clothing;

public sealed partial class ToggleableClothingRadialMenu : RadialMenu
{
    [Dependency] private EntityManager _entityManager = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IInputManager _inputManager = default!;
    private ToggleableClothingSystem _toggleable = default!;

    public event Action<EntityUid>? SendToggleClothingMessageAction;
    public EntityUid Entity { get; private set; }

    public ToggleableClothingRadialMenu()
    {
        IoCManager.InjectDependencies(this);
        RobustXamlLoader.Load(this);
        _toggleable = _entityManager.System<ToggleableClothingSystem>();
    }

    public void SetEntity(EntityUid uid)
    {
        Entity = uid;
        RefreshUI();
    }

    public void OpenOverMouseScreenPosition()
    {
        OpenCenteredAt(_inputManager.MouseScreenPosition.Position / _clyde.ScreenSize);
    }

    public void RefreshUI()
    {
        var main = FindControl<RadialContainer>("Main");
        main.DisposeAllChildren();

        if (!_entityManager.TryGetComponent<ToggleableClothingComponent>(Entity, out var clothing)
            || clothing.Container == null)
            return;

        foreach (var attached in clothing.ClothingUids)
        {
            var button = new ToggleableClothingRadialMenuButton
            {
                SetSize = new Vector2(64, 64),
                ToolTip = Loc.GetString(_toggleable.IsAttachedStored(Entity, attached.Key, clothing)
                    ? "toggleable-clothing-attach-tooltip"
                    : "toggleable-clothing-unattach-tooltip"),
                AttachedClothingId = attached.Key
            };

            var spriteView = new SpriteView
            {
                SetSize = new Vector2(48, 48),
                VerticalAlignment = VAlignment.Center,
                HorizontalAlignment = HAlignment.Center,
                Stretch = SpriteView.StretchMode.Fill
            };
            spriteView.SetEntity(attached.Key);
            button.AddChild(spriteView);
            button.OnPressed += _ =>
            {
                SendToggleClothingMessageAction?.Invoke(button.AttachedClothingId);
                main.DisposeAllChildren();
                RefreshUI();
            };
            main.AddChild(button);
        }
    }
}

public sealed class ToggleableClothingRadialMenuButton : RadialMenuButtonWithSector
{
    public EntityUid AttachedClothingId { get; set; }
}
