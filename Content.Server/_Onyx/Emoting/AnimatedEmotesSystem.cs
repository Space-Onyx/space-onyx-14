using Content.Shared.Chat;
using Content.Shared.Emoting;
using Robust.Shared.Prototypes;

namespace Content.Server.Emoting;

public sealed partial class AnimatedEmotesSystem : SharedAnimatedEmotesSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AnimatedEmotesComponent, EmoteEvent>(OnEmote);
    }

    private void OnEmote(Entity<AnimatedEmotesComponent> ent, ref EmoteEvent args)
    {
        ent.Comp.Emote = args.Emote.ID;
        Dirty(ent);
        if (args.Emote.TargetEvents is not null)
            foreach (var targetEvent in args.Emote.TargetEvents)
            {
                targetEvent.Target = ent;
                RaiseLocalEvent(ent, (object) targetEvent, true);
            }
    }
}
