// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 OnsenCapy <101037138+OnsenCapy@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Solstice <solsticeofthewinter@gmail.com>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 gluesniffler <linebarrelerenthusiast@gmail.com>
// SPDX-FileCopyrightText: 2025 loltart <lo1tartyt@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.CosmicCult.Components;
using Robust.Shared.Timing;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.StatusEffectNew.Components;
namespace Content.Server._Onyx.CosmicCult.EntitySystems;

/// <summary>
/// Makes the person with this component take damage over time.
/// Used for status effect.
/// </summary>
public sealed partial class CosmicEntropyDegenSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CosmicEntropyDebuffComponent, ComponentStartup>(OnInit);
        SubscribeLocalEvent<CosmicEntropyNonCultistComponent, ComponentStartup>(OnInitNonCultist); // Goobstation change. For non-cultist equipment debuff
    }

    private void OnInit(EntityUid uid, CosmicEntropyDebuffComponent comp, ref ComponentStartup args)
    {
        comp.CheckTimer = _timing.CurTime + comp.CheckWait;
    }

    // Goobstation change. For non-cultist equipment debuff
    private void OnInitNonCultist(EntityUid uid, CosmicEntropyNonCultistComponent comp, ref ComponentStartup args)
    {
        comp.CheckTimer = _timing.CurTime + comp.CheckWait;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CosmicEntropyDebuffComponent, StatusEffectComponent>();
        while (query.MoveNext(out _, out var component, out var status))
        {
            if (_timing.CurTime < component.CheckTimer || status.AppliedTo is not { } target)
                continue;

            component.CheckTimer = _timing.CurTime + component.CheckWait;
            _damageable.TryChangeDamage(target, component.Degen, true, false);
        }

        // Goobstation change. For non-cultist equipment Debuff
        var nonCultistQuery = EntityQueryEnumerator<CosmicEntropyNonCultistComponent, StatusEffectComponent>();
        while (nonCultistQuery.MoveNext(out _, out var component, out var status))
        {
            if (_timing.CurTime < component.CheckTimer || status.AppliedTo is not { } target)
                continue;

            component.CheckTimer = _timing.CurTime + component.CheckWait;
            _damageable.TryChangeDamage(target, component.Degen, true, false);
        }

    }
}
