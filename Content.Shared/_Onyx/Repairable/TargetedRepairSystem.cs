using Content.Shared._Onyx.Targeting;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Repairable;
using System.Linq;

namespace Content.Shared._Onyx.Repairable;

public sealed partial class TargetedRepairSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private TargetResolverSystem _resolver = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RepairableComponent, ResolveRepairPartEvent>(OnResolve);
        SubscribeLocalEvent<RepairableComponent, ValidateRepairPartEvent>(OnValidate);
    }

    private void OnResolve(Entity<RepairableComponent> body, ref ResolveRepairPartEvent args)
    {
        if (!TryComp(args.User, out TargetingComponent? targeting) || !_body.GetBodyChildren(body).Any())
            return;

        args.Targeted = true;
        if (_resolver.TryResolveExact(body, targeting.Target, out var part) && HasComp<DamageableComponent>(part))
            args.Part = part;
    }

    private void OnValidate(Entity<RepairableComponent> body, ref ValidateRepairPartEvent args)
    {
        args.Valid = _body.BodyHasChild(body, args.Part) && HasComp<DamageableComponent>(args.Part);
    }
}

[ByRefEvent]
public record struct ResolveRepairPartEvent(EntityUid User)
{
    public bool Targeted;
    public EntityUid? Part;
}

[ByRefEvent]
public record struct ValidateRepairPartEvent(EntityUid Part)
{
    public bool Valid;
}
