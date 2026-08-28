using Content.Shared.Body.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared._Onyx.Wounds;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Chemistry.Circulation;

/// <summary>
/// Applies an inner entity effect to each body part that belongs to the specified circulatory stream.
/// Allows any reagent effect to be stream-filtered without hardcoding per-stream variants.
/// </summary>
public sealed partial class CirculatoryStreamWrapperEffectSystem : EntityEffectSystem<WoundHostComponent, CirculatoryStreamWrapperEffect>
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private CirculatoryStreamSystem _circulation = default!;
    [Dependency] private SharedEntityEffectsSystem _entityEffects = default!;

    protected override void Effect(Entity<WoundHostComponent> entity, ref EntityEffectEvent<CirculatoryStreamWrapperEffect> args)
    {
        var stream = args.Effect.Stream;
        var inners = args.Effect.Effects.Count > 0 ? args.Effect.Effects : args.Effect.Effect != null ? new List<EntityEffect> { args.Effect.Effect } : null;
        if (inners == null || inners.Count == 0)
            return;

        var scale = args.Scale;
        foreach (var inner in inners)
        {
            if (inner == null)
                continue;

            var applied = false;
            foreach (var (part, _) in _body.GetBodyChildren(entity))
            {
                if (!TryComp(part, out WoundableComponent? woundable))
                    continue;
                if (_circulation.GetPartStream((part, woundable)) != stream)
                    continue;

                _entityEffects.ApplyEffect(part, inner, scale);
                applied = true;
            }

            if (!applied)
                _entityEffects.ApplyEffect(entity.Owner, inner, scale);
        }
    }
}

public sealed partial class CirculatoryStreamWrapperEffect : EntityEffectBase<CirculatoryStreamWrapperEffect>
{
    [DataField(required: true)]
    public ProtoId<CirculatoryStreamPrototype> Stream = default!;

    [DataField]
    public List<EntityEffect> Effects = new();

    [DataField]
    public EntityEffect? Effect;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var streamName = prototype.TryIndex(Stream, out var proto) ? proto.ID : Stream.Id;
        var innerEffects = new List<EntityEffect>();
        if (Effects.Count > 0)
            innerEffects.AddRange(Effects);
        else if (Effect != null)
            innerEffects.Add(Effect);

        var parts = new List<string>();
        foreach (var eff in innerEffects)
        {
            var txt = eff.EntityEffectGuidebookText(prototype, entSys);
            if (!string.IsNullOrWhiteSpace(txt))
                parts.Add(txt);
        }

        var innerText = parts.Count > 0 ? string.Join(", ", parts) : string.Empty;
        return Loc.GetString("entity-effect-guidebook-circulatory-stream-wrapper",
            ("stream", streamName),
            ("effect", innerText));
    }
}
