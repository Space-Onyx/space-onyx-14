using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Body;

/// <summary>
/// Components provided to a body while this organ is installed and functional.
/// </summary>
[RegisterComponent]
public sealed partial class FunctionalOrganComponent : Component
{
    [DataField(required: true)]
    public ComponentRegistry Components = new();
}

/// <summary>
/// Raised after an organ crosses the functional health threshold.
/// </summary>
[ByRefEvent]
public readonly record struct OrganFunctionChangedEvent(EntityUid Body, bool Functional);
