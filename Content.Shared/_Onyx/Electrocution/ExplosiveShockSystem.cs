// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Ilya246 <57039557+Ilya246@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Administration.Logs;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Electrocution;
using Content.Shared.Explosion.Components;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Timing;

namespace Content.Shared._Onyx.Electrocution;

public sealed partial class ExplosiveShockSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedExplosionSystem _explosion = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExplosiveShockComponent, InventoryRelayedEvent<ElectrocutionAttemptEvent>>(OnElectrocuted);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ExplosiveShockIgnitedComponent>();
        var now = _timing.CurTime;
        while (query.MoveNext(out var uid, out var ignited))
        {
            if (now >= ignited.ExplodeAt)
                TryExplode(uid);
        }
    }

    private void OnElectrocuted(EntityUid uid, ExplosiveShockComponent component,
        InventoryRelayedEvent<ElectrocutionAttemptEvent> args)
    {
        if (!HasComp<ExplosiveComponent>(uid))
            return;

        _popup.PopupEntity(Loc.GetString("explosive-shock-sizzle", ("item", uid)), uid);
        _adminLogger.Add(LogType.Electrocution,
            $"{ToPrettyString(args.Args.TargetUid):entity} triggered explosive shock item {ToPrettyString(uid):entity}");
        var ignited = EnsureComp<ExplosiveShockIgnitedComponent>(uid);
        ignited.ExplodeAt = _timing.CurTime + component.ExplosionDelay;
    }

    private void TryExplode(EntityUid uid)
    {
        if (Deleted(uid) ||
            !TryComp<ExplosiveComponent>(uid, out var explosive) ||
            !TryComp<ExplosiveShockComponent>(uid, out var component))
            return;

        EntityUid? wearer = null;
        if (TryComp<ClothingComponent>(uid, out var clothing) && clothing.InSlot != null)
            wearer = Transform(uid).ParentUid;

        _explosion.TriggerExplosive(uid, explosive);

        if (wearer == null)
            return;

        foreach (var part in _body.GetBodyChildrenOfType(wearer.Value, BodyPartType.Hand))
            _damageable.TryChangeDamage(part.Id, component.HandsDamage, true);

        foreach (var part in _body.GetBodyChildrenOfType(wearer.Value, BodyPartType.Arm))
            _damageable.TryChangeDamage(part.Id, component.ArmsDamage, true);

        _stun.TryKnockdown(wearer.Value, component.KnockdownTime, true);
    }
}
