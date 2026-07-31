using Content.Server.DeviceLinking.Systems;
using Content.Server.Electrocution;
using Content.Server.Power.EntitySystems;
using Content.Shared.Buckle.Components;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.ExecutionChair;

public sealed partial class ExecutionChairSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private DeviceLinkSystem _device = default!;
    [Dependency] private ElectrocutionSystem _shock = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExecutionChairComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ExecutionChairComponent, SignalReceivedEvent>(OnSignal);
    }

    private void OnMapInit(Entity<ExecutionChairComponent> ent, ref MapInitEvent args) =>
        _device.EnsureSinkPorts(ent, ent.Comp.TogglePort, ent.Comp.OnPort, ent.Comp.OffPort);

    private void OnSignal(Entity<ExecutionChairComponent> ent, ref SignalReceivedEvent args)
    {
        var enabled = args.Port == ent.Comp.TogglePort ? !ent.Comp.Enabled :
            args.Port == ent.Comp.OnPort ? true :
            args.Port == ent.Comp.OffPort ? false : ent.Comp.Enabled;
        ent.Comp.Enabled = enabled;
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString(enabled ? "execution-chair-turn-on" : "execution-chair-turn-off"), ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<ExecutionChairComponent, StrapComponent>();
        while (query.MoveNext(out var uid, out var chair, out var strap))
        {
            if (!chair.Enabled || !Transform(uid).Anchored || !this.IsPowered(uid, EntityManager) ||
                chair.NextDamageTick > _timing.CurTime || strap.BuckledEntities.Count == 0)
                continue;

            foreach (var target in strap.BuckledEntities)
            {
                var volume = _random.NextFloat(0.8f, 1.2f);
                if (_shock.TryDoElectrocution(target, uid, chair.DamagePerTick, TimeSpan.FromSeconds(chair.DamageTime),
                        true, volume, ignoreInsulation: true) && chair.PlaySoundOnShock)
                    _audio.PlayPvs(chair.ShockNoises, target, AudioParams.Default.WithVolume(chair.ShockVolume));
            }
            chair.NextDamageTick = _timing.CurTime + TimeSpan.FromSeconds(1);
        }
    }
}
