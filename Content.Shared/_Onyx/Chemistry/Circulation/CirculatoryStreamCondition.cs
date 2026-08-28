using Content.Shared.Body.Systems;
using Content.Shared.EntityConditions;
using Content.Shared.Localizations;
using Content.Shared._Onyx.Wounds;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Chemistry.Circulation;

public sealed partial class CirculatoryStreamCondition : EntityConditionBase<CirculatoryStreamCondition>
{
    [DataField(required: true)]
    public ProtoId<CirculatoryStreamPrototype> Stream = default!;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
    {
        if (!prototype.TryIndex(Stream, out var proto))
            return Loc.GetString("entity-condition-guidebook-circulatory-stream",
                ("stream", Stream.Id),
                ("shouldhave", !Inverted));

        return Loc.GetString("entity-condition-guidebook-circulatory-stream",
            ("stream", proto.ID),
            ("shouldhave", !Inverted));
    }
}

public sealed partial class CirculatoryStreamConditionSystem : EntityConditionSystem<WoundHostComponent, CirculatoryStreamCondition>
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private CirculatoryStreamSystem _circulation = default!;

    protected override void Condition(Entity<WoundHostComponent> entity, ref EntityConditionEvent<CirculatoryStreamCondition> args)
    {
        foreach (var (part, _) in _body.GetBodyChildren(entity))
        {
            if (!TryComp(part, out WoundableComponent? woundable))
                continue;

            if (_circulation.GetPartStream((part, woundable)) == args.Condition.Stream)
            {
                args.Result = true;
                return;
            }
        }

        args.Result = false;
    }
}
