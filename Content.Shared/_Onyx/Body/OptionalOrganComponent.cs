using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Body;

/// <summary>
/// Prevents an organ from spawning until one of its profile marking layers is selected.
/// </summary>
[RegisterComponent]
[Access(typeof(OptionalOrganSystem), typeof(InitialBodySystem))]
public sealed partial class OptionalOrganComponent : Component
{
    [DataField(required: true)]
    public BodyPartType ParentPart;

    [DataField(required: true)]
    public HashSet<HumanoidVisualLayers> Layers = [];
}

/// <summary>
/// Marks an optional organ created from profile markings, not by surgery.
/// </summary>
[RegisterComponent]
public sealed partial class ProfileGeneratedOrganComponent : Component;

public sealed partial class OptionalOrganSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VisualBodyComponent, BeforeVisualBodyMarkingsAppliedEvent>(OnBeforeMarkingsApplied);
        SubscribeLocalEvent<ProfileGeneratedOrganComponent, OrganGotRemovedEvent>(OnRemoved);
    }

    private void OnBeforeMarkingsApplied(Entity<VisualBodyComponent> ent, ref BeforeVisualBodyMarkingsAppliedEvent args)
    {
        if (!TryComp<InitialBodyComponent>(ent, out var initialBody))
            return;

        foreach (var (category, prototype) in initialBody.GetOptionalOrganPrototypes())
        {
            var entityPrototype = ProtoMan.Index<EntityPrototype>(prototype);
            if (!entityPrototype.TryComp<OptionalOrganComponent>(out var optional, EntityManager.ComponentFactory))
                continue;

            var slot = category.Id;
            var hasMarking = args.Markings.TryGetValue(category, out var organMarkings)
                && optional.Layers.Any(layer => organMarkings.TryGetValue(layer, out var markings) && markings.Count != 0);
            var parent = _body.GetBodyChildrenOfType(ent, optional.ParentPart).FirstOrDefault().Id;
            if (!parent.IsValid())
                continue;

            var hasOrgan = _body.TryGetOrganInSlot(parent, slot, out var organ);
            if (!hasMarking)
            {
                if (hasOrgan
                    && HasComp<ProfileGeneratedOrganComponent>(organ)
                    && _body.TryRemoveOrgan(parent, slot, out organ, reparent: false))
                {
                    Del(organ);
                }
                continue;
            }

            if (hasOrgan)
                continue;

            organ = Spawn(prototype, Transform(ent).Coordinates);
            EnsureComp<ProfileGeneratedOrganComponent>(organ);
            if (!_body.TryInsertOrgan(parent, organ, slot))
                Del(organ);
        }
    }

    private void OnRemoved(Entity<ProfileGeneratedOrganComponent> ent, ref OrganGotRemovedEvent args)
    {
        RemCompDeferred<ProfileGeneratedOrganComponent>(ent);
    }
}
