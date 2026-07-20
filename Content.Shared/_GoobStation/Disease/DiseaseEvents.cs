using Content.Shared._GoobStation.Disease.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Disease;

[ByRefEvent] public record struct DiseaseUpdateEvent(Entity<DiseaseCarrierComponent> Ent);
[ByRefEvent] public record struct DiseaseGainedEvent(Entity<DiseaseComponent> Disease);
[ByRefEvent] public record struct DiseaseCuredEvent(Entity<DiseaseComponent> Disease);
[ByRefEvent] public record struct DiseaseCloneEvent(Entity<DiseaseComponent> Source);
[ByRefEvent] public record struct DiseaseInfectAttemptEvent(Entity<DiseaseComponent> Disease, bool CanInfect = true);
[ByRefEvent] public record struct GetImmunityEvent(Entity<DiseaseComponent> Disease, float ImmunityGainRate = 0f, float ImmunityStrength = 0f);
[ByRefEvent] public record struct DiseaseEffectEvent(DiseaseEffectComponent Comp, Entity<DiseaseComponent> Disease, Entity<DiseaseCarrierComponent> Ent);
[ByRefEvent] public record struct DiseaseEffectFailedEvent(DiseaseEffectComponent Comp, Entity<DiseaseComponent> Disease, Entity<DiseaseCarrierComponent> Ent);
[ByRefEvent] public record struct DiseaseCheckConditionsEvent(DiseaseEffectComponent Comp, Entity<DiseaseComponent> Disease, Entity<DiseaseCarrierComponent> Ent, bool DoEffect = true);

public abstract record DiseaseSpreadAttemptEvent(float Power, float Chance, ProtoId<DiseaseSpreadPrototype> Type)
{
    public float Power { get; set; } = Power;
    public float Chance { get; set; } = Chance;
    public ProtoId<DiseaseSpreadPrototype> Type { get; } = Type;
    public void ApplyModifier(DiseaseSpreadModifier modifier) { Power += modifier.PowerMod(Type); Chance *= modifier.ChanceMult(Type); }
}

[ByRefEvent] public record DiseaseOutgoingSpreadAttemptEvent(float Power, float Chance, ProtoId<DiseaseSpreadPrototype> Type) : DiseaseSpreadAttemptEvent(Power, Chance, Type);
[ByRefEvent] public record DiseaseIncomingSpreadAttemptEvent(float Power, float Chance, ProtoId<DiseaseSpreadPrototype> Type) : DiseaseSpreadAttemptEvent(Power, Chance, Type);
