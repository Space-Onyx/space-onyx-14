using Content.Shared.Construction.Components;
using JetBrains.Annotations;
using Robust.Shared.Containers;
// <Onyx-TabletopComputers>
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
// </Onyx-TabletopComputers>

namespace Content.Shared.Construction.NodeEntities;

/// <summary>
///     Works for both <see cref="ComputerBoardComponent"/> and <see cref="MachineBoardComponent"/>
///     because duplicating code just for this is really stinky.
/// </summary>
[UsedImplicitly]
[DataDefinition]
public sealed partial class BoardNodeEntity : IGraphNodeEntity
{
    [DataField]
    public string Container { get; private set; } = string.Empty;

    // <Onyx-TabletopComputers>
    [DataField]
    public string? PrototypeSuffix { get; private set; }
    // </Onyx-TabletopComputers>

    public string? GetId(EntityUid? uid, EntityUid? userUid, GraphNodeEntityArgs args)
    {
        if (uid == null)
            return null;

        var containerSystem = args.EntityManager.EntitySysManager.GetEntitySystem<SharedContainerSystem>();

        if (!containerSystem.TryGetContainer(uid.Value, Container, out var container)
            || container.ContainedEntities.Count == 0)
            return null;

        var board = container.ContainedEntities[0];

        // There should not be a case where more than one of these components exist on the same entity
        if (args.EntityManager.TryGetComponent(board, out MachineBoardComponent? machine))
            return machine.Prototype;

        // <Onyx-TabletopComputers-edited>
        if (args.EntityManager.TryGetComponent(board, out ComputerBoardComponent? computer))
            return GetComputerPrototype(computer.Prototype);
        // </Onyx-TabletopComputers-edited>

        if (args.EntityManager.TryGetComponent(board, out ElectronicsBoardComponent? electronics))
            return electronics.Prototype;

        return null;
    }

    // <Onyx-TabletopComputers>
    private string? GetComputerPrototype(string? prototype)
    {
        if (string.IsNullOrWhiteSpace(prototype) || string.IsNullOrWhiteSpace(PrototypeSuffix))
            return prototype;

        var suffixedPrototype = $"{prototype}{PrototypeSuffix}";
        var prototypeManager = IoCManager.Resolve<IPrototypeManager>();

        return prototypeManager.HasIndex<EntityPrototype>(suffixedPrototype)
            ? suffixedPrototype
            : prototype;
    }
    // </Onyx-TabletopComputers>
}
