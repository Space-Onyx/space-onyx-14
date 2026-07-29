using Content.Shared._Onyx.CollectiveMind;

namespace Content.Shared.Chat;

public abstract partial class SharedChatSystem
{
    public const char CollectiveMindPrefix = '+';

    public bool TryProcessCollectiveMindMessage(
        EntityUid source,
        string input,
        out string output,
        out CollectiveMindPrototype? channel,
        bool quiet = false)
    {
        output = input.Trim();
        channel = null;

        if (!input.StartsWith(CollectiveMindPrefix))
            return false;

        if (!TryComp<CollectiveMindComponent>(source, out var mind))
            return true;

        var defaultChannel = mind.DefaultChannel;
        if (input.Length < 2 || char.IsWhiteSpace(input[1]))
        {
            output = SanitizeMessageCapital(input[1..].TrimStart());
            if (defaultChannel != null)
                ProtoMan.TryIndex(defaultChannel, out channel);
            else if (!quiet)
                _popup.PopupEntity(Loc.GetString("collective-mind-no-default-channel"), source, source);

            return true;
        }

        var keyCode = input[1].ToString();
        output = SanitizeMessageCapital(input[2..].TrimStart());
        if (defaultChannel is { } defaultId &&
            ProtoMan.TryIndex(defaultId, out CollectiveMindPrototype? defaultPrototype) &&
            string.Equals(defaultPrototype.KeyCode.ToString(), keyCode, StringComparison.OrdinalIgnoreCase))
            channel = defaultPrototype;
        else
        {
            foreach (var id in mind.Channels)
            {
                if (!ProtoMan.TryIndex(id, out CollectiveMindPrototype? prototype) ||
                    !string.Equals(prototype.KeyCode.ToString(), keyCode, StringComparison.OrdinalIgnoreCase))
                    continue;

                channel = prototype;
                break;
            }
        }

        if (channel == null && !quiet)
            _popup.PopupEntity(Loc.GetString("collective-mind-no-such-channel", ("key", input[1])), source, source);

        return true;
    }
}
