using Content.Shared._Onyx.Speech;
using Content.Shared._Onyx.Surgery.Organs;
using Content.Shared.Body;

namespace Content.Server._Onyx.Surgery.Organs;

public sealed class TongueSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TongueComponent, OrganGotInsertedEvent>(OnTongueInserted);
        SubscribeLocalEvent<TongueComponent, OrganGotRemovedEvent>(OnTongueRemoved);
    }

    private void OnTongueInserted(Entity<TongueComponent> ent, ref OrganGotInsertedEvent args)
    {
        RemComp<TonguelessAccentComponent>(args.Target);
    }

    private void OnTongueRemoved(Entity<TongueComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (TerminatingOrDeleted(args.Target))
            return;

        EnsureComp<TonguelessAccentComponent>(args.Target);
    }
}
