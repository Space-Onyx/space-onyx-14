using Content.Shared.Alert;
using Content.Shared.Body.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Wounds;

public sealed partial class FractureAlertSystem : EntitySystem
{
    private static readonly ProtoId<AlertPrototype> BrokenBonesAlert = "BrokenBones";

    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private WoundFractureSystem _fractures = default!;

    public void Refresh(EntityUid? body)
    {
        if (body is not { } uid)
            return;

        foreach (var (part, _) in _body.GetBodyChildren(uid))
        {
            if (_fractures.GetFracture(part) is { Comp2.Grade: not FractureGrade.None,
                                                  Comp2.Treatment: not FractureTreatment.Mended })
            {
                _alerts.ShowAlert(uid, BrokenBonesAlert);
                return;
            }
        }

        _alerts.ClearAlert(uid, BrokenBonesAlert);
    }
}
