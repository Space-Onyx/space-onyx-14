using Content.Shared._Onyx.MartialArts;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;

namespace Content.Shared.Weapons.Melee;

public abstract partial class SharedMeleeWeaponSystem
{
    private float CalculateShoveStaminaDamage(EntityUid user, EntityUid target)
    {
        var damage = TryComp<ShovingComponent>(user, out var shoving)
            ? shoving.StaminaDamage
            : ShovingComponent.DefaultStaminaDamage;

        if (!TryComp<PhysicsComponent>(user, out var userPhysics) ||
            !TryComp<PhysicsComponent>(target, out var targetPhysics) ||
            userPhysics.InvMass <= 0f ||
            targetPhysics.InvMass <= 0f)
            return damage;

        return damage * Math.Clamp(targetPhysics.InvMass / userPhysics.InvMass, 0f, 2f);
    }

    private void RaiseSelfDisarmCombo(EntityUid user, EntityUid meleeUid)
    {
        RaiseLocalEvent(user,
            new ComboAttackPerformedEvent(user, user, meleeUid, ComboAttackType.Disarm));
    }
}
