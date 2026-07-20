using Content.Shared._Onyx.Wounds;
using Content.Shared.Body.Systems;
using Robust.Shared.Utility;

namespace Content.Shared.HealthExaminable;

public sealed partial class HealthExaminableSystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private PainSystem _pain = default!;

    private void AddPainMarkup(EntityUid examined, EntityUid examiner, FormattedMessage message)
    {
        if (examined != examiner)
            return;

        foreach (var (part, _) in _body.GetBodyChildren(examined))
        {
            if (!TryComp(part, out PainComponent? pain))
                continue;

            var value = _pain.GetPain((part, pain));
            var level = value >= 50 ? "agony"
                : value >= 30 ? "terrible"
                : value >= 15 ? "strong"
                : value > 0 ? "light"
                : null;
            if (level == null)
                continue;

            if (!message.IsEmpty)
                message.PushNewline();
            message.AddMarkupOrThrow(Loc.GetString($"health-examinable-pain-{level}", ("part", Name(part))));
        }
    }
}
