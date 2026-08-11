using Content.Shared._Onyx.Storage;
using Content.Shared.Nutrition;
using Content.Shared.Speech;
using Content.Shared.Speech.EntitySystems;

namespace Content.Server._Onyx.Storage;

public sealed partial class MouthStorageSystem : SharedMouthStorageSystem
{
    [Dependency] private MumbleAccentSystem _mumble = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MouthStorageComponent, AccentGetEvent>(OnAccent);
        SubscribeLocalEvent<MouthStorageComponent, IngestionAttemptEvent>(OnIngestionAttempt);
    }

    private void OnAccent(Entity<MouthStorageComponent> ent, ref AccentGetEvent args)
    {
        if (TryGetOccupiedMouth(ent.Comp, out _))
            args.Message = _mumble.Accentuate(args.Message, null);
    }

    private void OnIngestionAttempt(Entity<MouthStorageComponent> ent, ref IngestionAttemptEvent args)
    {
        if (!TryGetOccupiedMouth(ent.Comp, out var storage))
            return;

        args.Blocker = storage.Container.ContainedEntities[0];
        args.Cancelled = true;
    }
}
