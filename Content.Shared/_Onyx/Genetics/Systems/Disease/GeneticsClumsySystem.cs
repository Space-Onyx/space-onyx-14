// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Shared.Genetics.Systems;

public sealed partial class GeneticsClumsySystem : EntitySystem
{
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    private static readonly EntProtoId ClumsyEffect = "StatusEffectClumsyAll";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GeneticsClumsyComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<GeneticsClumsyComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<GeneticsClumsyComponent> ent, ref ComponentStartup args)
    {
        _statusEffects.TrySetStatusEffectDuration(ent, ClumsyEffect);
    }

    private void OnShutdown(Entity<GeneticsClumsyComponent> ent, ref ComponentShutdown args)
    {
        _statusEffects.TryRemoveStatusEffect(ent, ClumsyEffect);
    }
}
