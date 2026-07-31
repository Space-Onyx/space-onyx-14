using System.Linq;
using Content.Shared._Onyx.Targeting;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;

namespace Content.Server._Onyx.Targeting;

public sealed partial class PartStatusSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private PainSystem _pain = default!;
    private float _refresh;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PartStatusComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<WoundableComponent, WoundCreatedEvent>(OnWoundChanged);
        SubscribeLocalEvent<WoundableComponent, WoundChangedEvent>(OnWoundChanged);
        SubscribeLocalEvent<WoundableComponent, WoundRemovedEvent>(OnWoundChanged);
        SubscribeLocalEvent<WoundableComponent, PartBleedingChangedEvent>(OnWoundChanged);
        SubscribeLocalEvent<WoundableComponent, FractureGradeChangedEvent>(OnWoundChanged);
        SubscribeLocalEvent<WoundableComponent, ScarCreatedEvent>(OnWoundChanged);
        SubscribeLocalEvent<PainComponent, PainChangedEvent>(OnPainChanged);
    }

    public override void Update(float frameTime)
    {
        _refresh += frameTime;
        if (_refresh < 1f)
            return;
        _refresh = 0f;
        var query = EntityQueryEnumerator<PartStatusComponent>();
        while (query.MoveNext(out var uid, out var status))
            Refresh((uid, status));
    }

    private void OnInit(Entity<PartStatusComponent> ent, ref ComponentInit args) => Refresh(ent);
    private void OnWoundChanged<T>(Entity<WoundableComponent> part, ref T args) where T : notnull
    {
        if (TryComp(part, out BodyPartComponent? bodyPart) && bodyPart.Body is { } body && TryComp(body, out PartStatusComponent? status))
            Refresh((body, status));
    }

    private void OnPainChanged(Entity<PainComponent> part, ref PainChangedEvent args)
    {
        if (TryComp(part, out BodyPartComponent? bodyPart) && bodyPart.Body is { } body && TryComp(body, out PartStatusComponent? status))
            Refresh((body, status));
    }

    public void Refresh(Entity<PartStatusComponent> ent)
    {
        var snapshot = new Dictionary<TargetBodyPart, PartStatus>();
        foreach (var target in SharedTargetingSystem.SelectableParts)
            snapshot[target] = Content.Shared._Onyx.Targeting.PartStatusSystem.Missing;

        foreach (var (part, bodyPart) in _body.GetBodyChildren(ent.Owner))
        {
            if (!SharedTargetingSystem.TryConvert(bodyPart.PartType, bodyPart.Symmetry, out var target))
                continue;
            var status = Snapshot(part);
            snapshot[target] = status;
            if (target == TargetBodyPart.Chest)
                snapshot[TargetBodyPart.Groin] = status;
        }

        if (ent.Comp.Parts.Count == snapshot.Count && snapshot.All(pair => ent.Comp.Parts.GetValueOrDefault(pair.Key) == pair.Value))
            return;
        ent.Comp.Parts = snapshot;
        Dirty(ent);
    }

    private PartStatus Snapshot(EntityUid part)
    {
        var damage = 0f;
        var bleeding = false;
        var fracture = FractureGrade.None;
        var scar = false;
        foreach (var wound in _wounds.GetWounds(part))
        {
            damage += wound.Comp.Severity.Float();
            bleeding |= CompOrNull<WoundBleedingComponent>(wound)?.CurrentRate > 0f;
            if (CompOrNull<WoundFractureComponent>(wound) is { } found && found.Grade > fracture)
                fracture = found.Grade;
            scar |= HasComp<WoundScarComponent>(wound);
        }
        var severity = Content.Shared._Onyx.Targeting.PartStatusSystem.GetSeverity(damage);
        if (TryComp(part, out PainComponent? pain))
            severity = (PartDamageSeverity) Math.Max((int) severity,
                (int) Content.Shared._Onyx.Targeting.PartStatusSystem.GetSeverity(_pain.GetPain((part, pain)).Float()));
        return new(true, severity, bleeding, fracture, scar);
    }
}
