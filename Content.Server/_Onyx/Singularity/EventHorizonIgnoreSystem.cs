using Content.Server.Singularity.Events;
using Content.Shared.Whitelist;

namespace Content.Server._Onyx.Singularity;

public sealed partial class EventHorizonIgnoreSystem : EntitySystem
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<EventHorizonIgnoreComponent, EventHorizonAttemptConsumeEntityEvent>(OnAttemptConsume);
    }

    private void OnAttemptConsume(Entity<EventHorizonIgnoreComponent> entity,
        ref EventHorizonAttemptConsumeEntityEvent args)
    {
        args.Cancelled |= _whitelist.IsWhitelistPassOrNull(entity.Comp.HorizonWhitelist, args.EventHorizonUid);
    }
}
