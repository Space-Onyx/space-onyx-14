using Content.Server.Wires;
using Content.Shared._Onyx.Contraband;
using Content.Shared.Wires;

namespace Content.Server._Onyx.Contraband;

[DataDefinition]
public sealed partial class ContrabandDetectorFakeScanWireAction : BaseToggleWireAction
{
    private ContrabandDetectorSystem _detector = default!;

    public override Color Color { get; set; } = Color.CadetBlue;
    public override string Name { get; set; } = "wire-name-contraband-detector-fake-scan";
    public override object? StatusKey { get; } = ContrabandDetectorFakeScanWireKey.StatusKey;
    public override object? TimeoutKey { get; } = ContrabandDetectorFakeScanWireKey.TimeoutKey;

    public override void Initialize()
    {
        base.Initialize();
        _detector = EntityManager.System<ContrabandDetectorSystem>();
    }

    public override StatusLightState? GetLightState(Wire wire) =>
        EntityManager.TryGetComponent(wire.Owner, out ContrabandDetectorComponent? component) && !component.IsFalseScanning
            ? StatusLightState.On
            : StatusLightState.Off;

    public override void ToggleValue(EntityUid owner, bool setting)
    {
        if (EntityManager.TryGetComponent(owner, out ContrabandDetectorComponent? component))
            _detector.ToggleFakeScanning((owner, component));
    }

    public override bool GetValue(EntityUid owner) =>
        EntityManager.TryGetComponent(owner, out ContrabandDetectorComponent? component) && !component.IsFalseScanning;
}

[DataDefinition]
public sealed partial class ContrabandDetectorBadChanceWireAction : BaseToggleWireAction
{
    private ContrabandDetectorSystem _detector = default!;

    public override Color Color { get; set; } = Color.DarkOrange;
    public override string Name { get; set; } = "wire-name-contraband-detector-chance";
    public override object? StatusKey { get; } = ContrabandDetectorChanceWireKey.StatusKey;
    public override object? TimeoutKey { get; } = ContrabandDetectorChanceWireKey.TimeoutKey;

    public override void Initialize()
    {
        base.Initialize();
        _detector = EntityManager.System<ContrabandDetectorSystem>();
    }

    public override StatusLightState? GetLightState(Wire wire) =>
        EntityManager.TryGetComponent(wire.Owner, out ContrabandDetectorComponent? component) && component.IsFalseDetectingChanged
            ? StatusLightState.BlinkingSlow
            : StatusLightState.On;

    public override void ToggleValue(EntityUid owner, bool setting)
    {
        if (EntityManager.TryGetComponent(owner, out ContrabandDetectorComponent? component))
            _detector.ChangeFalseDetectionChance((owner, component));
    }

    public override bool GetValue(EntityUid owner) =>
        EntityManager.TryGetComponent(owner, out ContrabandDetectorComponent? component) && !component.IsFalseDetectingChanged;
}
