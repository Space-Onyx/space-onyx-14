using Content.Shared.StatusEffectNew;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;

namespace Content.Shared._Onyx.Stealth.ForcedStealth;

public sealed partial class ForcedStealthSystem : EntitySystem
{
    [Dependency] private SharedStealthSystem _stealth = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ForcedStealthStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<ForcedStealthStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
    }

    private void OnApplied(Entity<ForcedStealthStatusEffectComponent> entity, ref StatusEffectAppliedEvent args)
    {
        var state = EnsureComp<ForcedStealthStateComponent>(args.Target);
        var first = state.ActiveOverrides.Count == 0;
        if (first)
            state.AddedStealth = !TryComp<StealthComponent>(args.Target, out _);

        TryComp<StealthComponent>(args.Target, out var stealth);
        stealth ??= EnsureComp<StealthComponent>(args.Target);
        if (first)
        {
            state.PreviousEnabled = stealth.Enabled;
            state.PreviousVisibility = _stealth.GetVisibility(args.Target, stealth);
        }

        state.ActiveOverrides.Add(entity.Owner);
        _stealth.SetEnabled(args.Target, true, stealth);
        _stealth.SetVisibility(args.Target, entity.Comp.Visibility, stealth);
    }

    private void OnRemoved(Entity<ForcedStealthStatusEffectComponent> entity, ref StatusEffectRemovedEvent args)
    {
        if (!TryComp<StealthComponent>(args.Target, out var stealth) ||
            !TryComp<ForcedStealthStateComponent>(args.Target, out var state))
            return;

        state.ActiveOverrides.Remove(entity.Owner);
        while (state.ActiveOverrides.Count > 0)
        {
            var active = state.ActiveOverrides[^1];
            if (TryComp<ForcedStealthStatusEffectComponent>(active, out var activeOverride))
            {
                _stealth.SetVisibility(args.Target, activeOverride.Visibility, stealth);
                return;
            }

            state.ActiveOverrides.RemoveAt(state.ActiveOverrides.Count - 1);
        }

        if (state.AddedStealth)
            RemCompDeferred<StealthComponent>(args.Target);
        else
        {
            _stealth.SetVisibility(args.Target, state.PreviousVisibility, stealth);
            _stealth.SetEnabled(args.Target, state.PreviousEnabled, stealth);
        }

        RemCompDeferred<ForcedStealthStateComponent>(args.Target);
    }
}
