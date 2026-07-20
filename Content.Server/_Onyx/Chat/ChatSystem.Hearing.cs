using Content.Shared._Onyx.Body;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    private bool CanHear(EntityUid entity)
    {
        return !HasComp<MissingEarsComponent>(entity);
    }
}
