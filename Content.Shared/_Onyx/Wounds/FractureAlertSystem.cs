using Content.Shared.Alert;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Wounds;

public sealed partial class FractureAlertSystem : EntitySystem
{
    private static readonly ProtoId<AlertPrototype> BrokenBonesAlert = "BrokenBones";

    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private WoundFractureSystem _fractures = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<WoundFractureComponent, FractureGradeChangedEvent>(OnGradeChanged);
        SubscribeLocalEvent<WoundFractureComponent, FractureTreatmentChangedEvent>(OnTreatmentChanged);
        SubscribeLocalEvent<WoundFractureComponent, WoundRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<WoundableComponent, OrganGotInsertedEvent>(OnPartInserted);
        SubscribeLocalEvent<WoundableComponent, OrganGotRemovedEvent>(OnPartRemoved);
    }

    private void OnGradeChanged(Entity<WoundFractureComponent> ent, ref FractureGradeChangedEvent args)
    {
        Refresh(args.Body);
    }

    private void OnTreatmentChanged(Entity<WoundFractureComponent> ent, ref FractureTreatmentChangedEvent args)
    {
        Refresh(args.Body);
    }

    private void OnRemoved(Entity<WoundFractureComponent> ent, ref WoundRemovedEvent args)
    {
        Refresh(CompOrNull<BodyPartComponent>(args.Part)?.Body);
    }

    private void OnPartInserted(Entity<WoundableComponent> ent, ref OrganGotInsertedEvent args)
    {
        Refresh(args.Target);
    }

    private void OnPartRemoved(Entity<WoundableComponent> ent, ref OrganGotRemovedEvent args)
    {
        Refresh(args.Target);
    }

    private void Refresh(EntityUid? body)
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
