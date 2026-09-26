// Content adapted from Goob-Station (https://github.com/Goob-Station/Goob-Station/pull/7076), licensed under AGPL-3.0-or-later.

using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared._Onyx.Construction;
using Content.Shared.Singularity.Components;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Onyx.Emitters;

public sealed partial class DiodeDiscSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DiodeDiscComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<DiodeDiscComponent, DiodeDiscDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<DiodeDiscComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        args.Handled = true;

        var user = args.User;
        if (!HasComp<EmitterComponent>(target))
            return;
        if (HasComp<UpgradedMachineComponent>(target))
            return;
        ent.Comp.SoundStream = _audio.PlayPredicted(ent.Comp.UpgradeSound, ent, user)?.Entity;
        Dirty(ent);
        var ev = new DiodeDiscDoAfterEvent();
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.Delay, ev, ent, target, ent));
    }

    private void OnDoAfter(Entity<DiodeDiscComponent> ent, ref DiodeDiscDoAfterEvent args)
    {
        ent.Comp.SoundStream = _audio.Stop(ent.Comp.SoundStream);

        if (args.Cancelled ||
            args.Handled ||
            args.Args.Target is not { } target ||
            !TryComp<EmitterComponent>(target, out var emitter) ||
            HasComp<UpgradedMachineComponent>(target))
            return;

        args.Handled = true;

        EntityManager.AddComponents(target, ent.Comp.ComponentsToAdd);
        PredictedQueueDel(ent);

        emitter.BoltType = ent.Comp.NewBolt;
    }
}
