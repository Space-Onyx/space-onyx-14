using Content.Goobstation.Shared.MartialArts;
using Content.Shared.Polymorph;

namespace Content.Server._Onyx.MartialArts;

public sealed partial class MartialArtsPolymorphSystem : EntitySystem
{
    [Dependency] private MartialArtsSystem _martialArts = default!;

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
            EnsureComp<NinjutsuSneakAttackComponent>(args.NewEntity).SurpriseReadyAt = ninja.SurpriseReadyAt;

        if (TryComp<DragonKungFuComponent>(args.OldEntity, out var dragon))
        {
            var copy = EnsureComp<DragonKungFuComponent>(args.NewEntity);
            copy.LastMoveTime = dragon.LastMoveTime;
            copy.PowerReady = dragon.PowerReady;
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
        }

        if (TryComp<SleepingCarpStudentComponent>(args.OldEntity, out var student))
        {
            var copy = EnsureComp<SleepingCarpStudentComponent>(args.NewEntity);
            copy.Stage = student.Stage;
            copy.UseAgainTime = student.UseAgainTime;
        }

        if (TryComp<KravMagaComponent>(args.OldEntity, out var krav))
        {
            var copy = EnsureComp<KravMagaComponent>(args.NewEntity);
            copy.Enabled = krav.Enabled;
            copy.SelectedMove = krav.SelectedMove;
            copy.SelectedStaminaDamage = krav.SelectedStaminaDamage;
            copy.SelectedEffectTime = krav.SelectedEffectTime;
        }
    }
}
