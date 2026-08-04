using Content.Shared._Onyx.Wounds;
using Content.Shared.Body.Systems;

namespace Content.Shared.HealthExaminable;

public sealed partial class HealthExaminableSystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private PainSystem _pain = default!;

    private string? GetPainLevel(EntityUid part)
    {
        if (!TryComp(part, out PainComponent? pain))
            return null;

        var value = _pain.GetPain((part, pain));
        return value >= 50 ? "agony"
            : value >= 30 ? "terrible"
            : value >= 15 ? "strong"
            : value > 0 ? "light"
            : null;
    }
}
