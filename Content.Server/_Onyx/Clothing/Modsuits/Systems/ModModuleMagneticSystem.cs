using Content.Shared._Onyx.Clothing.Modsuits.Components;
using Content.Shared.Alert;
using Content.Shared.Atmos.Components;
using Content.Shared.Clothing;
using Content.Shared.Gravity;
using Content.Shared.Inventory;

namespace Content.Server._Onyx.Clothing.Modsuits.Systems;

public sealed partial class ModModuleMagneticSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ModModuleMagneticComponent, ModModuleActivatedEvent>(OnActivated);
        SubscribeLocalEvent<ModModuleMagneticComponent, ModModuleDeactivatedEvent>(OnDeactivated);
        SubscribeLocalEvent<ModModuleMagneticComponent, ModModuleUninstalledEvent>(OnUninstalled);
        SubscribeLocalEvent<ModModuleMagneticEffectComponent, ComponentShutdown>(OnEffectShutdown);
    }

    private void OnActivated(Entity<ModModuleMagneticComponent> module, ref ModModuleActivatedEvent args)
    {
        var effect = EnsureComp<ModModuleMagneticEffectComponent>(args.Wearer);
        effect.Module = module.Owner;
        if (TryComp<MovedByPressureComponent>(args.Wearer, out var moved))
        {
            effect.OriginalPressureEnabled = moved.Enabled;
            moved.Enabled = false;
        }
        if (TryComp<MagbootsComponent>(FindBoots(args.Controller), out var boots))
            _alerts.ShowAlert(args.Wearer, boots.MagbootsAlert);
        _gravity.RefreshWeightless(args.Wearer);
    }

    private void OnDeactivated(Entity<ModModuleMagneticComponent> module, ref ModModuleDeactivatedEvent args)
    {
        if (args.Wearer is { } wearer)
            RemoveEffect(wearer, module.Owner, args.Controller);
    }

    private void OnUninstalled(Entity<ModModuleMagneticComponent> module, ref ModModuleUninstalledEvent args)
    {
        if (TryComp<Content.Shared._Onyx.Clothing.Components.SealableClothingControlComponent>(args.Controller, out var seal) &&
            seal.WearerEntity is { } wearer)
            RemoveEffect(wearer, module.Owner, args.Controller);
    }

    private void RemoveEffect(EntityUid wearer, EntityUid module, EntityUid controller)
    {
        if (!TryComp<ModModuleMagneticEffectComponent>(wearer, out var effect) || effect.Module != module)
            return;
        if (TryComp<MagbootsComponent>(FindBoots(controller), out var boots))
            _alerts.ClearAlert(wearer, boots.MagbootsAlert);
        RemComp<ModModuleMagneticEffectComponent>(wearer);
        _gravity.RefreshWeightless(wearer);
    }

    private void OnEffectShutdown(Entity<ModModuleMagneticEffectComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.OriginalPressureEnabled is { } enabled && TryComp<MovedByPressureComponent>(ent, out var moved))
            moved.Enabled = enabled;
    }

    private EntityUid FindBoots(EntityUid controller)
    {
        if (TryComp<Content.Shared.Clothing.Components.ToggleableClothingComponent>(controller, out var toggleable))
            foreach (var (part, slot) in toggleable.ClothingUids)
                if (slot == "shoes") return part;
        return controller;
    }
}
