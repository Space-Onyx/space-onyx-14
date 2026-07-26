using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared._Onyx.Body;
using Robust.Shared.Prototypes;

namespace Content.Shared.Body;

public sealed partial class InitialBodyComponent
{
    [Access(typeof(OptionalOrganSystem))]
    public IEnumerable<KeyValuePair<ProtoId<OrganCategoryPrototype>, EntProtoId>> GetOptionalOrganPrototypes()
    {
        return Organs;
    }
}

public sealed partial class InitialBodySystem
{
    private partial bool ShouldSkipInitialOrgan(EntProtoId prototype)
    {
        return ProtoMan.Index<EntityPrototype>(prototype).TryGetComponent<OptionalOrganComponent>(out _);
    }
}

public readonly record struct BeforeVisualBodyMarkingsAppliedEvent(
    Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> Markings);
