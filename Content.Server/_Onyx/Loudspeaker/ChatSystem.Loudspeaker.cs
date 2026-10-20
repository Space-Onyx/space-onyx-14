using Content.Shared._Onyx.Loudspeaker.Events;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    public int? GetLoudspeakerFontSize(EntityUid source, bool radio)
    {
        var getLoudspeakers = new GetLoudspeakerEvent();
        RaiseLocalEvent(source, ref getLoudspeakers);
        if (getLoudspeakers.Loudspeakers == null)
            return null;

        foreach (var loudspeaker in getLoudspeakers.Loudspeakers)
        {
            var data = new GetLoudspeakerDataEvent();
            RaiseLocalEvent(loudspeaker, ref data);
            if (data.IsActive && (radio ? data.AffectRadio : data.AffectChat))
                return data.FontSize;
        }

        return null;
    }
}
