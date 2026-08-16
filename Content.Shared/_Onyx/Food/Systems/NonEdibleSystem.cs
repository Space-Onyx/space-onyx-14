using Content.Shared._Onyx.Food.Components;
using Content.Shared.Nutrition;

namespace Content.Shared._Onyx.Food.Systems;

public sealed partial class NonEdibleSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NonEdibleComponent, IngestibleEvent>(OnIngestible);
    }

    private void OnIngestible(Entity<NonEdibleComponent> ent, ref IngestibleEvent args)
    {
        args.Cancelled = true;
    }
}
