using Content.Shared._Onyx.Medical.Surgery;
using Content.Shared._Onyx.Disease.Components;
using Content.Shared._Onyx.Disease.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Medical.Surgery;

public sealed partial class SurgeryInfectionSystem : EntitySystem
{
    private static readonly EntProtoId SurgicalInfection = "DiseaseSurgicalSiteInfection";
    private static readonly ProtoId<DamageTypePrototype> Poison = "Poison";
    private static readonly TimeSpan InfectionAttemptCooldown = TimeSpan.FromSeconds(30);

    private const float BaseInfectionChance = 0.65f;
    private const float MinimumComplexity = 12f;
    private const float MaximumComplexity = 24f;

    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedDiseaseSystem _disease = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    public void OnStep(ref SurgeryStepEvent args)
    {
        var chance = BaseInfectionChance;
        var sterile = false;

        var slots = _inventory.GetSlotEnumerator(args.User);
        while (slots.NextItem(out var item, out _))
        {
            if (!TryComp<SurgeryInfectionProtectionComponent>(item, out var protection))
                continue;

            chance *= protection.ChanceMultiplier;
            sterile = true;
        }

        if (!sterile)
            _damage.TryChangeDamage(args.Body,
                new DamageSpecifier(_prototypes.Index(Poison), 1),
                true,
                origin: args.User);

        TryInfect(args.Body, chance);
    }

    private void TryInfect(EntityUid body, float chance)
    {
        if (TryComp<DiseaseCarrierComponent>(body, out var carrier))
        {
            foreach (var diseaseUid in carrier.Diseases.ContainedEntities)
            {
                if (HasComp<SurgicalSiteInfectionComponent>(diseaseUid))
                    return;
            }
        }

        var cooldown = EnsureComp<SurgeryInfectionCooldownComponent>(body);
        if (_timing.CurTime < cooldown.NextAttempt)
            return;

        cooldown.NextAttempt = _timing.CurTime + InfectionAttemptCooldown;
        if (!_random.Prob(chance))
            return;

        var complexity = _random.NextFloat(MinimumComplexity, MaximumComplexity);
        var disease = _disease.MakeRandomDisease(SurgicalInfection, complexity, 0.2f);
        if (disease != null && !_disease.TryInfect(body, disease.Value))
            QueueDel(disease.Value);
    }
}
