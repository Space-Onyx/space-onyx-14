using Content.Shared._Onyx.Salvage.BookOfGreentext;
using Robust.Client.GameObjects;

namespace Content.Client._Onyx.Salvage.BookOfGreentext;

public sealed partial class BookOfGreentextSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CurseOfBookOfGreentextComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<CurseOfBookOfGreentextComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetColor((ent.Owner, sprite), Color.White);
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = AllEntityQuery<CurseOfBookOfGreentextComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var curse, out var sprite))
            _sprite.SetColor((uid, sprite), curse.Completed ? Color.LightGreen : Color.IndianRed);
    }
}
