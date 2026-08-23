using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Standing;
using Robust.Shared.GameStates;

namespace Content.Shared.Emoting;

public abstract class SharedAnimatedEmotesSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AnimatedEmotesComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<AnimatedEmotesComponent, BeforeEmoteEvent>(OnBeforeEmote);
    }

    private void OnGetState(Entity<AnimatedEmotesComponent> ent, ref ComponentGetState args)
    {
        args.State = new AnimatedEmotesComponentState(ent.Comp.Emote);
    }

    private void OnBeforeEmote(Entity<AnimatedEmotesComponent> ent, ref BeforeEmoteEvent args)
    {
        if (args.Emote.ID is not ("Flip" or "Spin" or "Jump" or "Tweak" or "Flex"))
            return;
        if (!TryComp<StandingStateComponent>(ent, out var standing) || !standing.Standing)
            args.Cancel();
    }
}
