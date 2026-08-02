using Content.Shared._Onyx.Materials;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Storage;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Shared._Onyx.Salvage.OreBag;

public sealed partial class OreBagSystem : EntitySystem
{
    [Dependency] private SharedMaterialStorageSystem _materials = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OreBagComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<OreBagComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !_timing.IsFirstTimePredicted || args.Target is not { } target ||
            !HasComp<SalvageMiningPointProcessorComponent>(target) ||
            !TryComp<StorageComponent>(ent, out var storage))
            return;

        var ores = storage.Container.ContainedEntities
            .Where(HasComp<MaterialComponent>)
            .ToArray();

        foreach (var ore in ores)
            _materials.TryInsertMaterialEntity(args.User, ore, target);

        args.Handled = ores.Length > 0;
    }
}
