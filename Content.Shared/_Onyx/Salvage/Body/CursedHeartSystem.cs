using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Onyx.Salvage.Body;

public sealed partial class CursedHeartSystem : EntitySystem
{
    private static readonly EntProtoId PumpAction = "ActionPumpCursedHeart";

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CursedHeartComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CursedHeartComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CursedHeartComponent, PumpHeartActionEvent>(OnPump);
        SubscribeLocalEvent<CursedHeartGrantComponent, UseInHandEvent>(OnUse);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<CursedHeartComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var heart, out var state))
        {
            if (state.CurrentState is MobState.Critical or MobState.Dead ||
                _timing.CurTime < heart.LastPump + TimeSpan.FromSeconds(heart.MaxDelay))
                continue;
            ChangeDamage(uid, new DamageSpecifier { DamageDict = { ["Asphyxiation"] = 25, ["Bloodloss"] = 25 } });
            _popup.PopupEntity(Loc.GetString("popup-cursed-heart-damage"), uid, uid, PopupType.MediumCaution);
            heart.LastPump = _timing.CurTime;
        }
    }

    private void OnMapInit(Entity<CursedHeartComponent> ent, ref MapInitEvent args)
        => _actions.AddAction(ent, ref ent.Comp.PumpActionEntity, PumpAction);

    private void OnShutdown(Entity<CursedHeartComponent> ent, ref ComponentShutdown args)
        => _actions.RemoveAction(ent.Owner, ent.Comp.PumpActionEntity);

    private void OnPump(Entity<CursedHeartComponent> ent, ref PumpHeartActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/_Onyx/Salvage/heartbeat.ogg"), ent);
        ChangeDamage(ent, new DamageSpecifier
        {
            DamageDict =
            {
                ["Blunt"] = FixedPoint2.New(-5f / 3f),
                ["Slash"] = FixedPoint2.New(-5f / 3f),
                ["Piercing"] = FixedPoint2.New(-5f / 3f),
                ["Asphyxiation"] = -2.5,
                ["Bloodloss"] = -2.5,
                ["Heat"] = -2,
                ["Shock"] = -2,
                ["Cold"] = -2,
                ["Caustic"] = -2,
            },
        });
        ent.Comp.LastPump = _timing.CurTime;
    }

    private void OnUse(Entity<CursedHeartGrantComponent> ent, ref UseInHandEvent args)
    {
        if (HasComp<CursedHeartComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("popup-cursed-heart-already-cursed"), args.User, args.User, PopupType.MediumCaution);
            args.Handled = true;
            return;
        }
        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/_Onyx/Salvage/heartbeat.ogg"), args.User);
        EnsureComp<CursedHeartComponent>(args.User).LastPump = _timing.CurTime;
        QueueDel(ent);
        args.Handled = true;
    }

    private void ChangeDamage(EntityUid uid, DamageSpecifier damage)
        => _damage.TryChangeDamage(uid, damage, true, false);
}
