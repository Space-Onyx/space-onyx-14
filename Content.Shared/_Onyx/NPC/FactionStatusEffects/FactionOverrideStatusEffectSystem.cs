using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.NPC.FactionStatusEffects;

public sealed partial class FactionOverrideStatusEffectSystem : EntitySystem
{
    [Dependency] private NpcFactionSystem _factions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FactionOverrideStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<FactionOverrideStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
    }

    private void OnApplied(Entity<FactionOverrideStatusEffectComponent> entity, ref StatusEffectAppliedEvent args)
    {
        if (!TryComp<NpcFactionMemberComponent>(args.Target, out var factions))
            return;

        var state = EnsureComp<FactionOverrideStateComponent>(args.Target);
        if (state.ActiveOverrides.Count == 0)
            state.OriginalFactions = new HashSet<ProtoId<NpcFactionPrototype>>(factions.Factions);

        state.ActiveOverrides.Add(entity.Owner);
        SetFaction((args.Target, factions), entity.Comp.Faction);
    }

    private void OnRemoved(Entity<FactionOverrideStatusEffectComponent> entity, ref StatusEffectRemovedEvent args)
    {
        if (!TryComp<NpcFactionMemberComponent>(args.Target, out var factions))
            return;

        if (!TryComp<FactionOverrideStateComponent>(args.Target, out var state))
            return;

        state.ActiveOverrides.Remove(entity.Owner);
        while (state.ActiveOverrides.Count > 0)
        {
            var active = state.ActiveOverrides[^1];
            if (TryComp<FactionOverrideStatusEffectComponent>(active, out var activeOverride))
            {
                SetFaction((args.Target, factions), activeOverride.Faction);
                return;
            }

            state.ActiveOverrides.RemoveAt(state.ActiveOverrides.Count - 1);
        }

        _factions.ClearFactions((args.Target, factions), false);
        _factions.AddFactions((args.Target, factions), state.OriginalFactions);
        RemCompDeferred<FactionOverrideStateComponent>(args.Target);
    }

    private void SetFaction(Entity<NpcFactionMemberComponent?> target, ProtoId<NpcFactionPrototype> faction)
    {
        _factions.ClearFactions(target, false);
        _factions.AddFaction(target, faction);
    }
}
