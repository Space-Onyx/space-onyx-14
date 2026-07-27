using Content.Server.Atmos.EntitySystems;
using Content.Shared.Armor;
using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Inventory;

namespace Content.Server._Onyx.Salvage.Pressure;

[RegisterComponent]
public sealed partial class PressureArmorChangeComponent : Component
{
    [DataField] public float LowerBound = Atmospherics.OneAtmosphere * 0.2f;
    [DataField] public float UpperBound = Atmospherics.OneAtmosphere * 0.5f;
    [DataField] public bool ApplyWhenInRange;
    [DataField] public float ExtraPenetrationModifier = 0.5f;
}

public sealed partial class PressureArmorChangeSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PressureArmorChangeComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<PressureArmorChangeComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnDamage,
            before: new[] { typeof(SharedArmorSystem) });
    }

    private void OnExamine(Entity<PressureArmorChangeComponent> ent, ref ExaminedEvent args) =>
        args.PushMarkup(Loc.GetString("salvage-pressure-armor-examine",
            ("modifier", Math.Round(ent.Comp.ExtraPenetrationModifier * 100))));

    private void OnDamage(Entity<PressureArmorChangeComponent> ent, ref InventoryRelayedEvent<DamageModifyEvent> args)
    {
        var pressure = _atmos.GetTileMixture((ent.Owner, Transform(ent)))?.Pressure ?? 0f;
        if ((pressure >= ent.Comp.LowerBound && pressure <= ent.Comp.UpperBound) != ent.Comp.ApplyWhenInRange ||
            !HasComp<ArmorComponent>(ent))
            return;
        args.Args.Damage.ArmorPenetration += ent.Comp.ExtraPenetrationModifier;
    }
}
