using Content.Server._Onyx.Lathe.Components;
using Content.Server.Materials;
using Content.Shared.Lathe;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Materials;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Lathe;

public sealed partial class LatheMaterialOutputSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private MaterialStorageSystem _materialStorage = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<LatheMaterialOutputComponent, LatheGetResultEvent>(OnLatheResult);
        SubscribeLocalEvent<LatheMaterialOutputComponent, GetMaterialWhitelistEvent>(OnGetMaterialWhitelist);
    }

    private void OnLatheResult(Entity<LatheMaterialOutputComponent> ent, ref LatheGetResultEvent args)
    {
        if (!TryComp<PhysicalCompositionComponent>(args.ResultItem, out var composition) ||
            !_materialStorage.TryChangeMaterialAmount(ent.Owner, composition.MaterialComposition))
            return;

        Del(args.ResultItem);
        args.Handled = true;
    }

    private void OnGetMaterialWhitelist(Entity<LatheMaterialOutputComponent> ent, ref GetMaterialWhitelistEvent args)
    {
        if (!TryComp<LatheComponent>(ent, out var lathe))
            return;

        foreach (var packId in lathe.StaticPacks)
        {
            if (!_prototype.TryIndex(packId, out var pack))
                continue;

            foreach (var recipeId in pack.Recipes)
            {
                if (!_prototype.TryIndex(recipeId, out var recipe) ||
                    recipe.Result is not { } resultId ||
                    !_prototype.TryIndex(resultId, out var result) ||
                    !result.TryGetComponent<PhysicalCompositionComponent>(out var composition, EntityManager.ComponentFactory))
                    continue;

                foreach (var material in composition.MaterialComposition.Keys)
                {
                    if (!args.Whitelist.Contains(material))
                        args.Whitelist.Add(material);
                }
            }
        }
    }
}
