using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Examine;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server._Onyx.Salvage.Pressure;

[RegisterComponent]
public sealed partial class PressureDamageChangeComponent : Component
{
    [DataField] public float LowerBound = 0;
    [DataField] public float UpperBound = Atmospherics.OneAtmosphere * 0.5f;
    [DataField] public bool ApplyWhenInRange = true;
    [DataField] public float AppliedModifier = 2f;
    [DataField] public bool ApplyToMelee = true; // Onyx: shared by melee salvage tools and PKAs.
    [DataField] public bool ApplyToProjectiles = true; // Onyx: PKA pressure behavior.
}

public sealed partial class PressureDamageChangeSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PressureDamageChangeComponent, GetMeleeDamageEvent>(OnMelee);
        SubscribeLocalEvent<PressureDamageChangeComponent, ExaminedEvent>(OnExamine);
    }
    private void OnMelee(Entity<PressureDamageChangeComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (ent.Comp.ApplyToMelee && IsActive(ent))
            args.Damage *= ent.Comp.AppliedModifier;
    }
    // Onyx-start: ranged pressure is handled post-expansion by PKAPressureUpgradeSystem.
    private bool IsActive(Entity<PressureDamageChangeComponent> ent)
    {
        var pressure = _atmos.GetTileMixture((ent.Owner, Transform(ent)))?.Pressure ?? 0f;
        return (pressure >= ent.Comp.LowerBound && pressure <= ent.Comp.UpperBound) == ent.Comp.ApplyWhenInRange;
    }
    // Onyx-end
    private void OnExamine(Entity<PressureDamageChangeComponent> ent, ref ExaminedEvent args) =>
        args.PushMarkup(Loc.GetString("salvage-pressure-damage-examine", ("modifier", ent.Comp.AppliedModifier)));
}
