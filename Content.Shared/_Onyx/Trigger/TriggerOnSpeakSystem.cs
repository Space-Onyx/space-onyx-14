using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Containers;

namespace Content.Shared._Onyx.Trigger;

public sealed partial class TriggerOnSpeakSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private TriggerSystem _trigger = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TriggerOnSpeakComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<TriggerOnSpeakComponent, ListenEvent>(OnListen);
    }

    private void OnInit(Entity<TriggerOnSpeakComponent> ent, ref ComponentInit args)
    {
        EnsureComp<ActiveListenerComponent>(ent).Range = ent.Comp.ListenRange;
    }

    private void OnListen(Entity<TriggerOnSpeakComponent> ent, ref ListenEvent args)
    {
        if (args.Source == ent.Owner ||
            _container.TryGetContainingContainer(ent.Owner, out var container) && container.Owner == args.Source)
        {
            _trigger.Trigger(ent, args.Source);
        }
    }
}
