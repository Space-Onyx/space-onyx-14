using Content.Shared.Damage;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Damage;

/// <summary>
/// Applies damage on collision, optionally filtering colliding entities.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DamageOnCollideComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier Damage;

    [DataField]
    public EntityWhitelist? IgnoreWhitelist;

    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Apply damage to the colliding entity instead of this entity.
    /// </summary>
    [DataField]
    public bool Inverted;
}
