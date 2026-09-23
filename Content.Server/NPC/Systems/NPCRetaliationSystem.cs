using Content.Server.NPC.Components;
using Content.Server._Onyx.NPC; // <Onyx-NPCRetaliation>
using Content.Shared._Onyx.Wounds; // <Onyx-NPCRetaliation>
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.Collections;
using Robust.Shared.Timing;

namespace Content.Server.NPC.Systems;

/// <summary>
///     Handles NPC which become aggressive after being attacked.
/// </summary>
public sealed partial class NPCRetaliationSystem : EntitySystem
{
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        SubscribeLocalEvent<NPCRetaliationComponent, DamageDealtEvent>(OnDamageDealt, before: [typeof(WoundDamageRoutingSystem)]); // <Onyx-NPCRetaliation-edited>
        SubscribeLocalEvent<NPCRetaliationComponent, DisarmedEvent>(OnDisarmed);
    }

    // <Onyx-NPCRetaliation-edited>
    private void OnDamageDealt(Entity<NPCRetaliationComponent> ent, ref DamageDealtEvent args)
    {
        if (args.Damage.GetTotal() <= 0)
            return;

        if (args.Origin is not {} origin)
            return;

        TryRetaliate(ent, origin);
    }
    // </Onyx-NPCRetaliation-edited>

    private void OnDisarmed(Entity<NPCRetaliationComponent> ent, ref DisarmedEvent args)
    {
        TryRetaliate(ent, args.Source);
    }

    public bool TryRetaliate(Entity<NPCRetaliationComponent> ent, EntityUid target, bool secondary = false) // <Onyx-NPCRetaliation-edited>
    {
        // don't retaliate against inanimate objects.
        if (!HasComp<MobStateComponent>(target))
            return false;

        // don't retaliate against the same faction
        if (_npcFaction.IsEntityFriendly(ent.Owner, target))
            return false;

        _npcFaction.AggroEntity(ent.Owner, target);
        if (ent.Comp.AttackMemoryLength is {} memoryLength)
            ent.Comp.AttackMemories[target] = _timing.CurTime + memoryLength;

        RaiseLocalEvent(ent, new NPCRetaliatedEvent(ent, target, secondary)); // <Onyx-NPCRetaliation>

        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NPCRetaliationComponent, FactionExceptionComponent>();
        while (query.MoveNext(out var uid, out var retaliationComponent, out var factionException))
        {
            // TODO: can probably reuse this allocation and clear it
            foreach (var entity in new ValueList<EntityUid>(retaliationComponent.AttackMemories.Keys))
            {
                if (!TerminatingOrDeleted(entity) && _timing.CurTime < retaliationComponent.AttackMemories[entity])
                    continue;

                _npcFaction.DeAggroEntity((uid, factionException), entity);
                // TODO: should probably remove the AttackMemory, thats the whole point of the ValueList right??
            }
        }
    }
}
