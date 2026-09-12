using System.Linq;
using Content.Shared._Onyx.Construction; // <Onyx-TieredMachineParts>
using Content.Shared.Construction.Components;
using Content.Shared.Examine;
using Content.Shared.Lathe;
using Content.Shared.Materials;

namespace Content.Shared.Construction
{
    /// <summary>
    /// Deals with machine parts and machine boards.
    /// </summary>
    public sealed partial class MachinePartSystem : EntitySystem
    {
        [Dependency] private SharedLatheSystem _lathe = default!;
        [Dependency] private SharedConstructionSystem _construction = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MachineBoardComponent, ExaminedEvent>(OnMachineBoardExamined);
        }

        private void OnMachineBoardExamined(EntityUid uid, MachineBoardComponent component, ExaminedEvent args)
        {
            if (!args.IsInDetailsRange)
                return;

            using (args.PushGroup(nameof(MachineBoardComponent)))
            {
                args.PushMarkup(Loc.GetString("machine-board-component-on-examine-label"));
                foreach (var (material, amount) in component.StackRequirements)
                {
                    if (material == TieredMachinePartRequirements.LegacyManipulator
                        && TieredMachinePartRequirements.ReplacesManipulators(component)) // <Onyx-TieredMachineParts>
                        continue;

                    var stack = ProtoMan.Index(material);
                    var name = ProtoMan.Index(stack.Spawn).Name;

                    args.PushMarkup(Loc.GetString("machine-board-component-required-element-entry-text",
                        ("amount", amount),
                        ("requiredElement", Loc.GetString(name))));
                }

                // <Onyx-TieredMachineParts>
                var partRequirements = new Dictionary<MachinePartKind, int>();
                TieredMachinePartRequirements.CopyFromBoard(component, partRequirements);
                foreach (var (kind, amount) in partRequirements)
                {
                    args.PushMarkup(Loc.GetString("machine-board-component-required-element-entry-text",
                        ("amount", amount),
                        ("requiredElement", Loc.GetString($"tiered-machine-part-kind-{kind.ToString().ToLowerInvariant()}"))));
                }
                // </Onyx-TieredMachineParts>

                foreach (var (_, info) in component.ComponentRequirements)
                {
                    var examineName = _construction.GetExamineName(info);
                    args.PushMarkup(Loc.GetString("machine-board-component-required-element-entry-text",
                        ("amount", info.Amount),
                        ("requiredElement", examineName)));
                }

                foreach (var (_, info) in component.TagRequirements)
                {
                    var examineName = _construction.GetExamineName(info);
                    args.PushMarkup(Loc.GetString("machine-board-component-required-element-entry-text",
                        ("amount", info.Amount),
                        ("requiredElement", examineName)));
                }
            }
        }

        public bool TryGetMachineBoardMaterialCost(Entity<MachineBoardComponent> entity, out Dictionary<string, int> materials, int coefficient = 1)
        {
            var (_, comp) = entity;

            materials = new Dictionary<string, int>();

            foreach (var (stackId, amount) in comp.StackRequirements)
            {
                if (stackId == TieredMachinePartRequirements.LegacyManipulator
                    && TieredMachinePartRequirements.ReplacesManipulators(comp)) // <Onyx-TieredMachineParts>
                    continue;

                var stackProto = ProtoMan.Index(stackId);
                var defaultProto = ProtoMan.Index(stackProto.Spawn);

                if (defaultProto.TryComp<PhysicalCompositionComponent>(out var physComp, EntityManager.ComponentFactory))
                {
                    foreach (var (mat, matAmount) in physComp.MaterialComposition)
                    {
                        materials.TryAdd(mat, 0);
                        materials[mat] += matAmount * amount * coefficient;
                    }
                }
                else if (_lathe.TryGetRecipesFromEntity(stackProto.Spawn, out var recipes))
                {
                    var partRecipe = recipes[0];
                    if (recipes.Count > 1)
                        partRecipe = recipes.MinBy(p => p.Materials.Values.Sum());

                    foreach (var (mat, matAmount) in partRecipe!.Materials)
                    {
                        materials.TryAdd(mat, 0);
                        materials[mat] += matAmount * amount * coefficient;
                    }
                }
                else
                {
                    // The item has no material cost, so we cannot get the full cost.
                    return false;
                }
            }

            // <Onyx-TieredMachineParts>
            var partRequirements = new Dictionary<MachinePartKind, int>();
            TieredMachinePartRequirements.CopyFromBoard(comp, partRequirements);
            foreach (var (kind, amount) in partRequirements)
            {
                var prototype = kind switch
                {
                    MachinePartKind.Servo => "StandardServoDrive",
                    MachinePartKind.Capacitor => "StandardCapacitorModule",
                    MachinePartKind.MatterBin => "StandardMatterRecycler",
                    MachinePartKind.Scanner => "StandardScannerModule",
                    MachinePartKind.Laser => "StandardLaserModule",
                    _ => throw new ArgumentOutOfRangeException(),
                };

                if (!_lathe.TryGetRecipesFromEntity(prototype, out var recipes))
                    return false;

                var recipe = recipes.MinBy(p => p.Materials.Values.Sum())!;
                foreach (var (material, materialAmount) in recipe.Materials)
                {
                    materials.TryAdd(material, 0);
                    materials[material] += materialAmount * amount * coefficient;
                }
            }
            // </Onyx-TieredMachineParts>

            var genericPartInfo = comp.ComponentRequirements.Values.Concat(comp.TagRequirements.Values);
            foreach (var info in genericPartInfo)
            {
                var amount = info.Amount;
                var defaultProtoId = info.DefaultPrototype;

                if (_lathe.TryGetRecipesFromEntity(defaultProtoId, out var recipes))
                {
                    var partRecipe = recipes[0];
                    if (recipes.Count > 1)
                        partRecipe = recipes.MinBy(p => p.Materials.Values.Sum());

                    foreach (var (mat, matAmount) in partRecipe!.Materials)
                    {
                        materials.TryAdd(mat, 0);
                        materials[mat] += matAmount * amount * coefficient;
                    }
                }
                else if (ProtoMan.Resolve(defaultProtoId, out var defaultProto) &&
                         defaultProto.TryComp<PhysicalCompositionComponent>(out var physComp, EntityManager.ComponentFactory))
                {
                    foreach (var (mat, matAmount) in physComp.MaterialComposition)
                    {
                        materials.TryAdd(mat, 0);
                        materials[mat] += matAmount * amount * coefficient;
                    }
                }
                else
                {
                    // The item has no material cost, so we cannot get the full cost.
                    return false;
                }
            }

            // We were able to construct all elements of the recipe.
            return true;
        }
    }
}
