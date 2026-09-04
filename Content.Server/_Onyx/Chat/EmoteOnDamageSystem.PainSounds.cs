using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.StatusEffectNew;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Random;

namespace Content.Server.Chat.Systems;

public sealed partial class EmoteOnDamageSystem
{
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    private void HandlePainDamageEmote(EntityUid uid, EmoteOnDamageComponent component, DamageChangedEvent args)
    {
        var totalDamage = _damageable.GetTotalDamage(uid).Float();
        var totalDelta = totalDamage - component.LastTotalDamage;
        component.LastTotalDamage = totalDamage;

        if (component.EmotesThreshold.Count == 0 || totalDelta <= 0 ||
            component.LastEmoteTime + component.EmoteCooldown > _gameTiming.CurTime ||
            !_random.Prob(component.EmoteChance) ||
            TryComp<MobStateComponent>(uid, out var mob) && mob.CurrentState is MobState.Critical or MobState.Dead ||
            _statusEffects.TryEffectsWithComp<PainNumbnessStatusEffectComponent>(uid, out _))
            return;

        var pain = args.DamageDelta is null ? totalDelta : 0f;
        if (args.DamageDelta is { } delta)
        {
            foreach (var (type, amount) in delta.DamageDict)
            {
                if (component.AllowedDamageType.Contains(type))
                    pain += amount.Float();
            }
        }

        if (pain < component.PainThreshold)
            return;

        float? threshold = null;
        foreach (var candidate in component.EmotesThreshold.Keys)
        {
            if (totalDamage >= candidate && (threshold == null || candidate > threshold))
                threshold = candidate;
        }

        if (threshold == null || !component.EmotesThreshold.TryGetValue(threshold.Value, out var emotes) || emotes.Count == 0)
            return;

        var emote = _random.Pick(emotes);
        if (component.WithChat)
            _chatSystem.TryEmoteWithChat(uid, emote, component.HiddenFromChatWindow ? ChatTransmitRange.HideChat : ChatTransmitRange.Normal);
        else
            _chatSystem.TryEmoteWithoutChat(uid, emote);

        component.LastEmoteTime = _gameTiming.CurTime;
    }
}
