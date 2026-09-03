using Content.Shared._Onyx.Surgery.Augments.NeuroInterface;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._Onyx.Surgery.Augments;

public sealed partial class AugmentSystem
{
    private void InitializeStrength()
    {
        SubscribeLocalEvent<GetMeleeDamageEvent>(OnMeleeDamage);
        SubscribeLocalEvent<AugmentStrengthComponent, CollectNeuroInterfaceTooltipEvent>(OnCollectStrengthTooltip);
    }

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

    private void OnCollectStrengthTooltip(
        Entity<AugmentStrengthComponent> ent,
        ref CollectNeuroInterfaceTooltipEvent args)
    {
        args.AddSection(
            "effect",
            Loc.GetString("neuro-interface-tooltip-section-effect"),
            Loc.GetString("neuro-interface-tooltip-strength-effect",
                ("value", MathF.Round((ent.Comp.Modifier - 1f) * 100f))));
    }
}
