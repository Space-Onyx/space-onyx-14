using Content.Shared._Onyx.MartialArts;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;

namespace Content.Server._Onyx.MartialArts;

public sealed partial class MartialArtsSystem : SharedMartialArtsSystem
{
    [Dependency] private ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CanPerformComboComponent, SleepingCarpSaying>(OnCarpSaying);
    }

    private void OnCarpSaying(Entity<CanPerformComboComponent> ent, ref SleepingCarpSaying args)
        => _chat.TrySendInGameICMessage(ent, Loc.GetString(args.Saying), InGameICChatType.Speak, false);
}
