// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Milon <milonpl.git@proton.me>
// SPDX-FileCopyrightText: 2025 OnsenCapy <101037138+OnsenCapy@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Solstice <solsticeofthewinter@gmail.com>
// SPDX-FileCopyrightText: 2025 TheBorzoiMustConsume <197824988+TheBorzoiMustConsume@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later


using Content.Server.Database;
using Content.Server.Ghost;
using Content.Shared._Onyx.CosmicCult;
using Content.Shared._Onyx.CosmicCult.Components;
using Content.Shared.Alert;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC;
using Content.Shared.Popups;
using Content.Shared.Light.Components;
using Content.Shared.StatusEffectNew;
using Robust.Server.Player;
using Robust.Shared.Random;

namespace Content.Server._Onyx.CosmicCult.Abilities;

public sealed partial class CosmicSiphonSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private CosmicCultRuleSystem _cultRule = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private GhostSystem _ghost = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private CosmicCultSystem _cosmicCult = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private HolyProtectionSystem _divineIntervention = default!;

    private readonly HashSet<Entity<PoweredLightComponent>> _lights = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CosmicCultComponent, EventCosmicSiphon>(OnCosmicSiphon);
        SubscribeLocalEvent<CosmicCultComponent, EventCosmicSiphonDoAfter>(OnCosmicSiphonDoAfter);
    }

    private void OnCosmicSiphon(Entity<CosmicCultComponent> uid, ref EventCosmicSiphon args)
    {
        if (uid.Comp.EntropyStored >= uid.Comp.EntropyStoredCap)
        {
            _popup.PopupEntity(Loc.GetString("cosmicability-siphon-full"), uid, uid);
            return;
        }
        if (_divineIntervention.TouchSpellDenied(args.Target))
            return;
        if (HasComp<ActiveNPCComponent>(args.Target) || TryComp<MobStateComponent>(args.Target, out var state) && state.CurrentState != MobState.Alive)
        {
            _popup.PopupEntity(Loc.GetString("cosmicability-siphon-fail", ("target", Identity.Entity(args.Target, EntityManager))), uid, uid);
            return;
        }
        if (args.Handled)
            return;

        var doargs = new DoAfterArgs(EntityManager, uid, uid.Comp.CosmicSiphonDelay, new EventCosmicSiphonDoAfter(), uid, args.Target)
        {
            DistanceThreshold = 2.5f,
            Hidden = true,
            BreakOnHandChange = false,
            BreakOnDamage = false,
            BreakOnMove = false,
            BreakOnDropItem = false,
        };
        args.Handled = true;
        _doAfter.TryStartDoAfter(doargs);
    }

    private void OnCosmicSiphonDoAfter(Entity<CosmicCultComponent> uid, ref EventCosmicSiphonDoAfter args)
    {
        if (args.Args.Target is not { } target
            || args.Cancelled
            || args.Handled)
            return;
        args.Handled = true;

        var requested = uid.Comp.CosmicSiphonQuantity * (uid.Comp.CosmicEmpowered ? 2 : 1);
        var gained = Math.Min(requested, Math.Max(0, uid.Comp.EntropyStoredCap - uid.Comp.EntropyStored));
        if (gained == 0)
            return;

        if (_mind.TryGetMind(uid, out var _, out var mind) && _player.TryGetSessionById(mind.UserId, out var session))
            RaiseNetworkEvent(new CosmicSiphonIndicatorEvent(GetNetEntity(target)), session);

        uid.Comp.EntropyStored += gained;
        uid.Comp.EntropyBudget += gained;
        Dirty(uid, uid.Comp);

        _statusEffects.TrySetStatusEffectDuration(target, "EntropicDegen", uid.Comp.CosmicEntropyDebuffDuration);

        if (_cosmicCult.EntityIsCultist(target))
        {
            _popup.PopupEntity(Loc.GetString("cosmicability-siphon-cultist-success",
                ("target", Identity.Entity(target, EntityManager))),
                uid,
                uid);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("cosmicability-siphon-success", ("target", Identity.Entity(target, EntityManager))), uid, uid);
            _alerts.ShowAlert(uid.Owner, uid.Comp.EntropyAlert);
            _cultRule.IncrementCultObjectiveEntropy(uid);
        }

        if (uid.Comp.CosmicEmpowered) // if you're empowered there's a 20% chance to flicker lights on siphon
        {
            _lights.Clear();
            _lookup.GetEntitiesInRange<PoweredLightComponent>(Transform(uid).Coordinates, uid.Comp.FlickerRange, _lights, LookupFlags.StaticSundries);
            foreach (var light in _lights) // static range of 5. because.
            {
                if (!_random.Prob(uid.Comp.FlickerProbability))
                    continue;

                _ghost.DoGhostBooEvent(light);
            }
        }
    }
}
