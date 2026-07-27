using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.CrusherUpgrades;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TrailComponent : Component
{
    [DataField, AutoNetworkedField] public Color Color = Color.White;
    [DataField, AutoNetworkedField] public float Scale = 1f;
    [DataField, AutoNetworkedField] public float Frequency = 0.2f;
    [DataField, AutoNetworkedField] public float Lifetime = 1f;
    [DataField, AutoNetworkedField] public float LerpTime = 0.05f;
    [DataField, AutoNetworkedField] public float AlphaLerpAmount = 0.3f;
    [DataField, AutoNetworkedField] public float ScaleLerpAmount;
    [DataField, AutoNetworkedField] public string? Shader;
    [DataField, AutoNetworkedField] public SpriteSpecifier? Sprite;
}

[RegisterComponent]
public sealed partial class HomingProjectileComponent : Component
{
    [DataField] public float HomingSpeed = 720f;
    [DataField] public Angle Tolerance = Angle.FromDegrees(1);
    [DataField] public float HomingTime = 0.1f;
    public float Accumulator;
}
