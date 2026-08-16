using Content.Shared.StatusEffectNew.Components;

namespace Content.Shared.StatusEffectNew;

/// <summary>
/// Keeps permanent status effects from <see cref="PermanentStatusEffectsComponent"/> applied
/// for as long as the owning component exists.
/// </summary>
public sealed partial class PermanentStatusEffectsSystem : EntitySystem
{
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    // <Onyx-OrganEffects>
    // Applies the effects when the component is added at runtime (e.g. via an organ's onAdd),
    // since MapInit won't fire for runtime-added components.
    [SubscribeLocalEvent]
    private void OnComponentInit(Entity<PermanentStatusEffectsComponent> ent, ref ComponentInit args)
    {
        foreach (var effect in ent.Comp.StatusEffects)
        {
            _statusEffects.TrySetStatusEffectDuration(ent, effect);
        }
    }
    // </Onyx-OrganEffects>

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<PermanentStatusEffectsComponent> ent, ref MapInitEvent args)
    {
        foreach (var effect in ent.Comp.StatusEffects)
        {
            _statusEffects.TrySetStatusEffectDuration(ent, effect);
        }
    }

    [SubscribeLocalEvent]
    private void OnRemove(Entity<PermanentStatusEffectsComponent> ent, ref ComponentRemove args)
    {
        foreach (var effect in ent.Comp.StatusEffects)
        {
            _statusEffects.TryRemoveStatusEffect(ent, effect);
        }
    }
}
