using Content.Server.Chat.Systems;
using Content.Server.NPC.Systems;
using Content.Shared._Onyx.MobCall;
using Content.Shared.Whitelist;

namespace Content.Server._Onyx.MobCall;

public sealed partial class MobCallSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private NPCSteeringSystem _steering = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobCallSourceComponent, MobCallActionEvent>(OnCall);
    }

    private void OnCall(Entity<MobCallSourceComponent> ent, ref MobCallActionEvent args)
    {
        if (args.Handled)
            return;

        _chat.TryEmoteWithChat(ent, ent.Comp.Emote, forceEmote: false);
        var target = Transform(ent).Coordinates;
        foreach (var callable in _lookup.GetEntitiesInRange<MobCallableComponent>(target, ent.Comp.Range))
        {
            if (!_whitelist.IsWhitelistPass(ent.Comp.Whitelist, callable))
                continue;

            var steering = _steering.Register(callable, target);
            steering.Range = 0.5f;
        }

        args.Handled = true;
    }
}
