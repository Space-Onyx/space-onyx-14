using Content.Shared.Chemistry.Reaction;
using Content.Shared.EntityEffects;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.EntityEffects.Effects.Chemistry;

public sealed partial class MakeUnreactiveEntityEffectSystem : EntityEffectSystem<ReactiveComponent, MakeUnreactive>
{
    private static readonly ProtoId<TagPrototype> TrashTag = "Trash";

    [Dependency] private TagSystem _tags = default!;

    protected override void Effect(Entity<ReactiveComponent> entity, ref EntityEffectEvent<MakeUnreactive> args)
    {
        RemComp<ReactiveComponent>(entity);
        _tags.AddTag(entity.Owner, TrashTag);
    }
}

public sealed partial class MakeUnreactive : EntityEffectBase<MakeUnreactive>;
