using System.Linq;
using Content.Shared._Onyx.Clothing.Components;
using Content.Shared._Onyx.Clothing.Modsuits.Components;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;

namespace Content.Server._Onyx.Clothing.Modsuits.Systems;

public sealed partial class ModModuleStorageSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ModModuleStorageComponent, ModModuleInstalledEvent>(OnInstalled);
        SubscribeLocalEvent<ModModuleStorageComponent, ModModuleUninstalledEvent>(OnUninstalled);
    }

    private void OnInstalled(Entity<ModModuleStorageComponent> module, ref ModModuleInstalledEvent args)
    {
        if (module.Comp.OriginalGrid != null || !TryComp<StorageComponent>(module, out var source) ||
            !TryComp<StorageComponent>(args.Controller, out var controller))
            return;
        module.Comp.OriginalGrid = new(controller.Grid);
        controller.Grid = new(source.Grid);
        _storage.RefreshStorageGrid((args.Controller, controller));
        Dirty(module);
    }

    private void OnUninstalled(Entity<ModModuleStorageComponent> module, ref ModModuleUninstalledEvent args)
    {
        if (module.Comp.OriginalGrid is not { } original || !TryComp<StorageComponent>(args.Controller, out var storage))
            return;
        var destination = TryComp<SealableClothingControlComponent>(args.Controller, out var seal) && seal.WearerEntity is { } wearer
            ? Transform(wearer).Coordinates
            : Transform(args.Controller).Coordinates;
        foreach (var item in storage.Container.ContainedEntities.ToArray())
            _containers.Remove(item, storage.Container, destination: destination);
        storage.Grid = new(original);
        _storage.RefreshStorageGrid((args.Controller, storage));
        module.Comp.OriginalGrid = null;
        Dirty(module);
    }
}
