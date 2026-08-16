using Content.Server.Power.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Bed.Components;
using Content.Shared._Onyx.Medical.Surgery;
using Robust.Shared.GameObjects;

namespace Content.Server._Onyx.Body;

/// <summary>
/// Determines whether an entity is being kept in a death-delaying state:
/// either buckled to a powered stasis bed or to an operating table.
/// A stasis operating table inherits both components, so it is covered either way.
/// </summary>
public static class BodyStasis
{
    public static bool IsActive(IEntityManager entMan, EntityUid body)
    {
        if (!entMan.TryGetComponent(body, out BuckleComponent? buckle) || buckle.BuckledTo is not { } target)
            return false;

        if (entMan.HasComponent<OperatingTableComponent>(target))
            return true;

        return entMan.TryGetComponent(target, out StasisBedComponent? _) &&
               entMan.TryGetComponent(target, out ApcPowerReceiverComponent? power) &&
               power.Powered;
    }
}