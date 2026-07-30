using Content.Server.Gateway.Components;
using Content.Shared.Tag;

namespace Content.Server.Gateway.Systems;

public sealed partial class GatewaySystem
{
    [Dependency] private TagSystem _tag = default!;

    private bool IsGatewayPairAllowed(EntityUid sourceUid, GatewayComponent source, EntityUid destinationUid, GatewayComponent destination)
    {
        return (source.TagRestriction == null || _tag.HasTag(destinationUid, source.TagRestriction.Value)) &&
               (destination.TagRestriction == null || _tag.HasTag(sourceUid, destination.TagRestriction.Value));
    }
}
