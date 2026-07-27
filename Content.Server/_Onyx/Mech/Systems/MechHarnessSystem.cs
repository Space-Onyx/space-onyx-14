using Content.Server._Onyx.Mech.Components;
using Content.Shared.Construction;
using Content.Shared.Construction.Components;
using Content.Shared.Interaction;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Robust.Server.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Mech.Systems;

public sealed partial class MechHarnessSystem : EntitySystem
{
    private static readonly ProtoId<ToolQualityPrototype> PryingQuality = "Prying";

    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private PartAssemblySystem _partAssembly = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MechHarnessComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(EntityUid uid, MechHarnessComponent component, InteractUsingEvent args)
    {
        if (args.Handled ||
            !_tool.HasQuality(args.Used, PryingQuality) ||
            !TryComp<PartAssemblyComponent>(uid, out var assembly) ||
            assembly.CurrentAssembly is not { } assemblyId ||
            _partAssembly.IsAssemblyFinished(uid, assemblyId, assembly))
        {
            return;
        }

        _container.EmptyContainer(assembly.PartsContainer);
        args.Handled = true;
    }
}
