using System.Numerics;
using Content.Shared.Body.Part;
// <Onyx-SlimeSurgery>
using Content.Shared._Onyx.Medical.Surgery;
// </Onyx-SlimeSurgery>
using Content.Shared.Damage.Components;
using Content.Shared.Humanoid;
// <Onyx-BodyConsequences>
using Content.Shared._Onyx.Body;
using Content.Shared.Body.Systems; // <Onyx-TransplantCompatibility>
// </Onyx-BodyConsequences>
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared.Body;

public sealed partial class InitialBodySystem : EntitySystem
{
    // <Onyx-Surgery>
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private OrganRelationSystem _organRelation = default!;
    [Dependency] private SharedBodySystem _body = default!; // <Onyx-TransplantCompatibility>

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InitialBodyComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<InitialBodyComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<ContainerManagerComponent>(ent, out var containerComp))
            return;

        if (TerminatingOrDeleted(ent) || !Exists(ent))
            return;

        if (!_container.TryGetContainer(ent, BodyComponent.RootContainerID, out var rootContainer, containerComp))
        {
            Log.Error($"Entity {ToPrettyString(ent)} with a {nameof(InitialBodyComponent)} is missing a container ({BodyComponent.RootContainerID}).");
            return;
        }

        var xform = Transform(ent);
        var coords = new EntityCoordinates(ent, Vector2.Zero);
        var spawned = new Dictionary<ProtoId<OrganCategoryPrototype>, EntityUid>();

        var external = new Dictionary<BodyPartType, Dictionary<BodyPartSymmetry, EntityUid>>();
        var internalOrgans = new List<(ProtoId<OrganCategoryPrototype> Category, EntProtoId Prototype)>();
        foreach (var (category, proto) in ent.Comp.Organs)
        {
            var spawn = Spawn(proto, coords);
            if (TryComp(spawn, out BodyPartComponent? part))
            {
                if (!external.TryGetValue(part.PartType, out var parts))
                    external[part.PartType] = parts = [];
                parts[part.Symmetry] = spawn; // <Onyx-OrganLifecycle-edited>
                // <Onyx-SurgeryPartDamage>
                part.Species = CompOrNull<HumanoidProfileComponent>(ent)?.Species;
                EnsureComp<DamageableComponent>(spawn);
                var injurable = EnsureComp<InjurableComponent>(spawn);
                injurable.DamageContainer = "Biological";
                Dirty(spawn, injurable);
                // </Onyx-SurgeryPartDamage>
                Dirty(spawn, part);
                continue;
            }

            if (HasComp<OrganComponent>(spawn))
                internalOrgans.Add((category, proto));

            Del(spawn);
        }

        // <Onyx-ChestGroin-edited>
        var rootType = BodyPartType.Chest;
        if (!external.TryGetValue(rootType, out var roots) || !roots.TryGetValue(BodyPartSymmetry.None, out var chest) || !_container.Insert(chest, rootContainer, containerXform: xform))
            return;

        Attach(external, chest, BodyPartType.Head, BodyPartSymmetry.None, "head");
        Attach(external, chest, BodyPartType.Arm, BodyPartSymmetry.Left, "left_arm");
        Attach(external, chest, BodyPartType.Arm, BodyPartSymmetry.Right, "right_arm");
        Attach(external, chest, BodyPartType.Groin, BodyPartSymmetry.None, "groin");
        var groin = external.GetValueOrDefault(BodyPartType.Groin)?.GetValueOrDefault(BodyPartSymmetry.None) ?? chest;
        Attach(external, groin, BodyPartType.Leg, BodyPartSymmetry.Left, "left_leg");
        Attach(external, groin, BodyPartType.Leg, BodyPartSymmetry.Right, "right_leg");
        Attach(external, external.GetValueOrDefault(BodyPartType.Arm)?.GetValueOrDefault(BodyPartSymmetry.Left), BodyPartType.Hand, BodyPartSymmetry.Left, "left_hand");
        Attach(external, external.GetValueOrDefault(BodyPartType.Arm)?.GetValueOrDefault(BodyPartSymmetry.Right), BodyPartType.Hand, BodyPartSymmetry.Right, "right_hand");
        Attach(external, external.GetValueOrDefault(BodyPartType.Leg)?.GetValueOrDefault(BodyPartSymmetry.Left), BodyPartType.Foot, BodyPartSymmetry.Left, "left_foot");
        Attach(external, external.GetValueOrDefault(BodyPartType.Leg)?.GetValueOrDefault(BodyPartSymmetry.Right), BodyPartType.Foot, BodyPartSymmetry.Right, "right_foot");

        foreach (var (category, proto) in internalOrgans) // <Onyx-OrganLifecycle-edited>
        {
            var spawn = Spawn(proto, coords);
            if (!TryComp(spawn, out OrganComponent? organ))
                continue;

            // <Onyx-SlimeSurgery-edited>
            var parent = HasComp<SlimeCoreComponent>(spawn)
                ? chest // <Onyx-SlimeCoreChest-edited>
                : HasComp<TorsoOrganComponent>(spawn)
                    ? chest
                : category.Id is "Brain" or "Eyes" or "Tongue" or "Ears"
                ? external.GetValueOrDefault(BodyPartType.Head)?.GetValueOrDefault(BodyPartSymmetry.None)
                : category.Id is "Liver" or "Kidneys" or "Appendix"
                    ? groin
                    : chest;
            // </Onyx-SlimeSurgery-edited>
            if (parent == null || !InsertOrgan(parent.Value, category.Id, spawn))
                Del(spawn);
        }
        _body.InitializeAnatomy(ent);
        // </Onyx-ChestGroin-edited>
    }

    // </Onyx-Surgery>
}
