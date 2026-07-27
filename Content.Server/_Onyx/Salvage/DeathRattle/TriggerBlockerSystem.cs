using Content.Server.Explosion.EntitySystems;
using Content.Shared.Trigger;
using Content.Shared.Whitelist;

namespace Content.Server._Onyx.Salvage.DeathRattle;

public sealed partial class TriggerBlockerSystem : EntitySystem
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TriggerBlockerComponent, AttemptTriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<TriggerBlockerComponent> ent, ref AttemptTriggerEvent args)
    {
        if (args.Cancelled)
            return;

        var map = Transform(ent).MapUid;
        if (map == null || _whitelist.CheckBoth(map.Value, ent.Comp.MapBlacklist, ent.Comp.MapWhitelist))
            return;

        args.Cancelled = true;
    }
}
