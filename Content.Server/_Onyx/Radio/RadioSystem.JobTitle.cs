using Content.Shared.Access.Systems;
using Content.Shared.Radio.Components;
using Robust.Shared.Utility;

namespace Content.Server.Radio.EntitySystems;

public sealed partial class RadioSystem
{
    [Dependency] private SharedIdCardSystem _idCard = default!;

    private string AddJobTitle(EntityUid messageSource, EntityUid radioSource, string name)
    {
        if (!TryComp<HeadsetComponent>(radioSource, out var headset) ||
            !_idCard.TryFindIdCard(messageSource, out var idCard) ||
            string.IsNullOrWhiteSpace(idCard.Comp.LocalizedJobTitle))
        {
            return name;
        }

        var jobTitle = FormattedMessage.EscapeText(idCard.Comp.LocalizedJobTitle);
        return Loc.GetString("chat-radio-job-title-wrap",
            ("color", headset.Color),
            ("jobTitle", jobTitle),
            ("name", name));
    }
}
