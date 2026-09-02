using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._Onyx.Surgery.Augments;

public sealed partial class AugmentSystem
{
    private void InitializeStrength() => SubscribeLocalEvent<GetMeleeDamageEvent>(OnMeleeDamage);

    private void OnMeleeDamage(ref GetMeleeDamageEvent args)
    {
        if (!TryComp(args.User, out InstalledAugmentsComponent? installed))
            return;
        foreach (var augment in ResolveAugments(installed))
        {
            if (TryComp(augment, out AugmentStrengthComponent? strength) && _toggle.IsActivated(augment) && IsEnabled(augment))
                args.Damage *= 1f + (strength.Modifier - 1f) * GetEfficiency(args.User, augment);
        }
    }
}
