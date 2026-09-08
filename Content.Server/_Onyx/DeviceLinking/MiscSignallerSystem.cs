using Content.Server.DeviceLinking.Systems;
using Content.Shared._Onyx.DeviceLinking;
using Content.Shared.Trigger;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.DeviceLinking;

public sealed partial class MiscSignallerSystem : EntitySystem
{
    [Dependency] private DeviceLinkSystem _link = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MiscSignallerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<MiscSignallerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnInit(Entity<MiscSignallerComponent> ent, ref ComponentInit args)
    {
        _link.EnsureSourcePorts(ent, ent.Comp.Port.Id);
    }

    private void OnTrigger(Entity<MiscSignallerComponent> ent, ref TriggerEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.NextActivation > _timing.CurTime)
            return;

        _link.InvokePort(ent, ent.Comp.Port.Id);
        ent.Comp.NextActivation = _timing.CurTime + ent.Comp.ActivationInterval;
    }
}
