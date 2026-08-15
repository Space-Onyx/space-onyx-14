using System.Linq;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;

namespace Content.Shared.Mobs.Systems;

public sealed partial class MobThresholdSystem
{
    [Dependency] private SharedBodySystem _body = default!;

    /// <summary>
    /// Calculates the total damage from vital body parts (Head, Chest, Groin, Torso), for complex bodies,
    /// including systemic damage. For non-complex bodies or if no vital parts are found, returns the total
    /// damage from the target entity.
    /// </summary>
    public FixedPoint2 CheckVitalDamage(EntityUid target, DamageableComponent damageableComponent)
    {
        if (!HasComp<WoundHostComponent>(target) ||
            !TryComp(target, out BodyComponent? body) ||
            body.RootContainer?.ContainedEntity is not { } rootPart)
            return _damageable.GetTotalDamage((damageableComponent.Owner, damageableComponent));

        var criticalParts = new[]
        {
            BodyPartType.Head,
            BodyPartType.Chest,
            BodyPartType.Groin,
            BodyPartType.Torso,
        };

        var result = FixedPoint2.Zero;
        foreach (var (part, partComponent) in _body.GetBodyChildren(target))
        {
            if (!TryComp(part, out DamageableComponent? partDamageable) ||
                !criticalParts.Contains(partComponent.PartType))
                continue;

            result += _damageable.GetTotalDamage((part, partDamageable));
        }

        if (TryComp(target, out SystemicDamageComponent? systemic))
            result += systemic.Damage.GetTotal();

        return result;
    }
}