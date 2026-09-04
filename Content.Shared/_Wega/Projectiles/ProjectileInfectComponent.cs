using Robust.Shared.Prototypes;

namespace Content.Shared.Projectiles;

[RegisterComponent]
public sealed partial class ProjectileInfectComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Infection;
    [DataField] public float Prob = 0.1f;
}
