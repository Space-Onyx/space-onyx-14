using Content.Shared.Trigger;

namespace Content.Server._Onyx.Trigger;

/// <summary>
/// Queues the parent for deletion when this entity is triggered.
/// </summary>
[RegisterComponent]
public sealed partial class DeleteParentOnTriggerComponent : Component;
