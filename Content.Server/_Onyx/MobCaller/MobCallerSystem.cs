using Content.Server.NPC;
using Content.Server.NPC.Systems;
using Content.Server.Power.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Linq;
using System.Numerics;

namespace Content.Server._Onyx.MobCaller;

public sealed partial class MobCallerSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobCallerComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<MobCallerComponent> ent, ref ExaminedEvent args)
    {
        bool occluded;
        if (ent.Comp.LastExamineRaycast + ent.Comp.ExamineRaycastSpacing > _timing.CurTime)
        {
            occluded = ent.Comp.CachedExamineResult;
        }
        else
        {
            occluded = GetSpawnDirections((ent, ent.Comp, Transform(ent))).Count == 0;
            ent.Comp.CachedExamineResult = occluded;
            ent.Comp.LastExamineRaycast = _timing.CurTime;
        }

        if (occluded)
            args.PushMarkup(Loc.GetString("mob-caller-occluded"));
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<MobCallerComponent>();
        while (query.MoveNext(out var uid, out var caller))
        {
            var xform = Transform(uid);
            if (caller.NeedPower && !_power.IsPowered(uid) || caller.NeedAnchored && !xform.Anchored)
                continue;

            caller.SpawnAccumulator += TimeSpan.FromSeconds(frameTime);
            if (caller.SpawnAccumulator < caller.SpawnSpacing)
                continue;

            caller.SpawnAccumulator -= caller.SpawnSpacing;
            caller.SpawnedEntities.RemoveAll(mob => TerminatingOrDeleted(mob) || _mobState.IsDead(mob));
            if (caller.SpawnedEntities.Count >= caller.MaxAlive)
                continue;

            var candidates = GetSpawnDirections((uid, caller, xform));
            if (candidates.Count == 0)
                continue;

            var spawnOffset = _random.Pick(candidates).ToVec() * _random.NextFloat(caller.MinDistance, caller.MaxDistance);
            var spawnPos = new MapCoordinates(xform.WorldPosition + spawnOffset, xform.MapID);
            if (_map.TryFindGridAt(spawnPos, out _, out _))
                continue;

            var spawned = Spawn(caller.SpawnProto, spawnPos);
            caller.SpawnedEntities.Add(spawned);
            _npc.SetBlackboard(spawned, NPCBlackboard.FollowTarget, new EntityCoordinates(uid, Vector2.Zero));
        }
    }

    public List<Angle> GetSpawnDirections(Entity<MobCallerComponent, TransformComponent> ent)
    {
        var candidates = new List<Angle>();
        for (var i = 0; i < ent.Comp1.SpawnDirections; i++)
        {
            var direction = Angle.FromDegrees(360f * i / ent.Comp1.SpawnDirections);
            if (IsDirectionClear(direction))
                candidates.Add(direction);
        }

        return candidates;

        bool IsDirectionClear(Angle direction)
        {
            var stepVector = direction.ToVec();
            var gridStep = stepVector * ent.Comp1.GridOcclusionFidelity;
            var steps = (int) MathF.Ceiling((ent.Comp1.GridOcclusionDistance - ent.Comp1.OcclusionDistance) /
                                           ent.Comp1.GridOcclusionFidelity);
            var checkPosition = ent.Comp2.WorldPosition + stepVector * ent.Comp1.OcclusionDistance;
            for (var i = 0; i < steps; i++)
            {
                if (_map.TryFindGridAt(new MapCoordinates(checkPosition, ent.Comp2.MapID), out _, out _))
                    return false;

                checkPosition += gridStep;
            }

            var ray = new CollisionRay(ent.Comp2.WorldPosition, stepVector, (int) ent.Comp1.OcclusionMask);
            return !_physics.IntersectRay(ent.Comp2.MapID, ray, ent.Comp1.OcclusionDistance, ent).Any();
        }
    }
}
