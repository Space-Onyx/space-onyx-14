using Content.Shared.Chasm;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Timing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared._Onyx.Salvage.Chasm;

public sealed partial class PreventChasmFallingSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private UseDelaySystem _delay = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChasmComponent, EntityStartFallingAttemptEvent>(OnStartFalling);
    }

    private void OnStartFalling(Entity<ChasmComponent> chasm, ref EntityStartFallingAttemptEvent args)
    {
        // Consuming a networked jaunter cannot be predicted: client deletion is rolled back and retriggers the chasm.
        if (_net.IsClient)
            return;

        if (!TryFindJaunter(args.Faller, out var jaunter) ||
            TryComp<UseDelayComponent>(jaunter, out var useDelay) && _delay.IsDelayed((jaunter, useDelay)))
            return;

        var origin = Transform(args.Faller).Coordinates;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var coords = new EntityCoordinates(Transform(args.Faller).ParentUid,
                origin.X + _random.NextFloat(-5f, 5f), origin.Y + _random.NextFloat(-5f, 5f));
            if (!_interaction.InRangeUnobstructed(args.Faller, coords, -1f) ||
                _lookup.GetEntitiesInRange<ChasmComponent>(coords, 1f).Count > 0)
                continue;

            args.Cancelled = true;
            _transform.SetCoordinates(args.Faller, coords);
            _transform.AttachToGridOrMap(args.Faller, Transform(args.Faller));
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Items/Mining/fultext_launch.ogg"), args.Faller);
            if (jaunter.Comp.DeleteOnUse)
            {
                RemComp<PreventChasmFallingComponent>(jaunter);
                QueueDel(jaunter);
            }
            else if (useDelay != null)
                _delay.TryResetDelay((jaunter, useDelay));
            return;
        }
    }

    private bool TryFindJaunter(EntityUid faller, out Entity<PreventChasmFallingComponent> jaunter)
    {
        if (TryComp<PreventChasmFallingComponent>(faller, out var direct))
        {
            jaunter = (faller, direct);
            return true;
        }

        foreach (var root in _inventory.GetHandOrInventoryEntities((faller,
                     CompOrNull<HandsComponent>(faller),
                     CompOrNull<InventoryComponent>(faller))))
        {
            if (TryFindInAccessibleContainers(root, out jaunter))
                return true;
        }

        jaunter = default;
        return false;
    }

    private bool TryFindInAccessibleContainers(EntityUid uid, out Entity<PreventChasmFallingComponent> jaunter)
    {
        if (TryComp<PreventChasmFallingComponent>(uid, out var component))
        {
            jaunter = (uid, component);
            return true;
        }

        if (!TryComp<StorageComponent>(uid, out var storage))
        {
            jaunter = default;
            return false;
        }

        foreach (var contained in storage.Container.ContainedEntities)
        {
            if (TryFindInAccessibleContainers(contained, out jaunter))
                return true;
        }

        jaunter = default;
        return false;
    }
}
