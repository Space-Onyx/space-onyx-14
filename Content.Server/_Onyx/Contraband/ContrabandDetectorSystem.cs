using Content.Server.DeviceLinking.Systems;
using Content.Server.Power.EntitySystems;
using Content.Shared._Onyx.Contraband;
using Robust.Server.Audio;
using Robust.Shared.Physics.Events;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Contraband;

public sealed partial class ContrabandDetectorSystem : SharedContrabandDetectorSystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private DeviceLinkSystem _deviceLink = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ContrabandDetectorComponent, StartCollideEvent>(OnCollide);
    }

    private void OnCollide(Entity<ContrabandDetectorComponent> detector, ref StartCollideEvent args)
    {
        if (!_power.IsPowered(detector) || detector.Comp.Scanned.ContainsKey(args.OtherEntity))
            return;

        detector.Comp.Scanned.Add(args.OtherEntity, _timing.CurTime + detector.Comp.ScanTimeOut);
        var detected = !detector.Comp.IsFalseScanning &&
                       (FindContraband(args.OtherEntity).Count > 0 ^ _random.Prob(detector.Comp.FalseDetectingChance));

        _audio.PlayPvs(detected ? detector.Comp.Detect : detector.Comp.NoDetect, detector);
        _deviceLink.SendSignal(detector, "SignalContrabandDetected", detected);
        detector.Comp.State = detected ? ContrabandDetectorState.Alarm : ContrabandDetectorState.Scan;
        detector.Comp.LastScanTime = _timing.CurTime;
        UpdateVisuals(detector);
        Dirty(detector);
    }
}
