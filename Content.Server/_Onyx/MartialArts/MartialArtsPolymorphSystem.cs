using Content.Shared._Onyx.MartialArts;
using Content.Shared.Alert;
using Content.Shared.Movement.Systems;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.MartialArts;

public sealed partial class MartialArtsPolymorphSystem : EntitySystem
{
    [Dependency] private MartialArtsSystem _martialArts = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ProtoId<AlertPrototype> DragonPowerAlert = "DragonPower";
    private static readonly ProtoId<AlertPrototype> SneakAttackAlert = "SneakAttack";
    private static readonly ProtoId<AlertPrototype> LossOfSurpriseAlert = "LossOfSurprise";

    public override void Initialize()
    {
        SubscribeLocalEvent<MartialArtsPolymorphComponent, PolymorphedEvent>(OnPolymorphed);
    }

    private void OnPolymorphed(Entity<MartialArtsPolymorphComponent> ent, ref PolymorphedEvent args)
    {
        EnsureComp<MartialArtsPolymorphComponent>(args.NewEntity);
        if (TryComp<MartialArtsKnowledgeComponent>(ent, out var knowledge))
        {
            var copyKnowledge = EnsureComp<MartialArtsKnowledgeComponent>(args.NewEntity);
            copyKnowledge.MartialArtsForm = knowledge.MartialArtsForm;
            copyKnowledge.Blocked = knowledge.Blocked;
            copyKnowledge.DamageBonus = knowledge.DamageBonus;
            Dirty(args.NewEntity, copyKnowledge);
            if (knowledge.MartialArtsForm == MartialArtsForms.SleepingCarp)
                _martialArts.GrantSleepingCarpEffects(args.NewEntity);
        }

        if (TryComp<CanPerformComboComponent>(args.OldEntity, out var combo))
        {
            var copy = EnsureComp<CanPerformComboComponent>(args.NewEntity);
            copy.LastAttacks = new(combo.LastAttacks);
            copy.CurrentTarget = combo.CurrentTarget;
            copy.ResetAfter = combo.ResetAfter;
            copy.ResetAt = combo.ResetAt;
            copy.ConsecutiveGnashes = combo.ConsecutiveGnashes;
            Dirty(args.NewEntity, copy);
        }

        if (TryComp<NinjutsuSneakAttackComponent>(args.OldEntity, out var ninja))
        {
            var copy = EnsureComp<NinjutsuSneakAttackComponent>(args.NewEntity);
            copy.SurpriseReadyAt = ninja.SurpriseReadyAt;
            copy.Multiplier = ninja.Multiplier;
            copy.AssassinateDamage = ninja.AssassinateDamage;
            copy.AssassinateUnarmedDamage = ninja.AssassinateUnarmedDamage;
            copy.TakedownSlowdownTime = ninja.TakedownSlowdownTime;
            copy.TakedownMuteTime = ninja.TakedownMuteTime;
            copy.TakedownSpeedModifier = ninja.TakedownSpeedModifier;
            copy.AssassinateSoundUnarmed = ninja.AssassinateSoundUnarmed;
            copy.AssassinateSoundArmed = ninja.AssassinateSoundArmed;
            if (_timing.CurTime < copy.SurpriseReadyAt)
                _alerts.ShowAlert(args.NewEntity, LossOfSurpriseAlert, cooldown: (_timing.CurTime, copy.SurpriseReadyAt));
            else
                _alerts.ShowAlert(args.NewEntity, SneakAttackAlert);
        }

        if (TryComp<DragonKungFuComponent>(args.OldEntity, out var dragon))
        {
            var copy = EnsureComp<DragonKungFuComponent>(args.NewEntity);
            copy.LastMoveTime = dragon.LastMoveTime;
            copy.BuffUntil = dragon.BuffUntil;
            copy.MinVelocitySquared = dragon.MinVelocitySquared;
            copy.PauseDuration = dragon.PauseDuration;
            copy.BuffLength = dragon.BuffLength;
            copy.AlertShown = _timing.CurTime < copy.BuffUntil;
            if (copy.AlertShown)
                _alerts.ShowAlert(args.NewEntity, DragonPowerAlert);
        }

        if (TryComp<MartialArtModifiersComponent>(args.OldEntity, out var modifiers))
        {
            var copy = EnsureComp<MartialArtModifiersComponent>(args.NewEntity);
            copy.AttackRate = modifiers.AttackRate;
            copy.Damage = modifiers.Damage;
            copy.MoveSpeed = modifiers.MoveSpeed;
            copy.AttackRateUntil = modifiers.AttackRateUntil;
            copy.DamageUntil = modifiers.DamageUntil;
            copy.MoveSpeedUntil = modifiers.MoveSpeedUntil;
            copy.DamageUnarmedOnly = modifiers.DamageUnarmedOnly;
            if (copy.MoveSpeed != 1f)
                _movement.RefreshMovementSpeedModifiers(args.NewEntity);
        }

        if (TryComp<SleepingCarpStudentComponent>(args.OldEntity, out var student))
        {
            var copy = EnsureComp<SleepingCarpStudentComponent>(args.NewEntity);
            copy.Stage = student.Stage;
            copy.UseAgainTime = student.UseAgainTime;
            copy.MinUseDelay = student.MinUseDelay;
            copy.MaxUseDelay = student.MaxUseDelay;
        }

        if (TryComp<KravMagaComponent>(args.OldEntity, out var krav))
        {
            var copy = EnsureComp<KravMagaComponent>(args.NewEntity);
            copy.Enabled = krav.Enabled;
            copy.SelectedMove = krav.SelectedMove;
            copy.SelectedStaminaDamage = krav.SelectedStaminaDamage;
            copy.SelectedEffectTime = krav.SelectedEffectTime;
            copy.BaseDamage = krav.BaseDamage;
            copy.DownedDamageModifier = krav.DownedDamageModifier;
        }
    }
}
