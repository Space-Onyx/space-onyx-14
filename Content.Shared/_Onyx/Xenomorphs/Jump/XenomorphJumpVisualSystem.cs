using Content.Shared.Movement.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Xenomorphs.Jump;

public sealed partial class XenomorphJumpVisualSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ActiveLeaperComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ActiveLeaperComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<ActiveLeaperComponent> entity, ref ComponentStartup args)
    {
        _appearance.SetData(entity, JumpVisuals.Jumping, true);
    }

    private void OnShutdown(Entity<ActiveLeaperComponent> entity, ref ComponentShutdown args)
    {
        _appearance.SetData(entity, JumpVisuals.Jumping, false);
    }
}

[Serializable, NetSerializable]
public enum JumpVisuals : byte
{
    Jumping,
}

public enum JumpLayers : byte
{
    Jumping,
}
