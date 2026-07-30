using Content.Shared.EntityTable;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Whitelist;

namespace Content.Server._Onyx.Salvage.Mobs;

public sealed partial class SpawnLootOnDeathSystem : EntitySystem
{
    [Dependency] private EntityTableSystem _entityTable = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SpawnLootOnDeathComponent, AttackedEvent>(OnAttacked);
        SubscribeLocalEvent<SpawnLootOnDeathComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnAttacked(Entity<SpawnLootOnDeathComponent> ent, ref AttackedEvent args)
    {
        if (ent.Comp.DoSpecialLoot)
            ent.Comp.DoSpecialLoot = _whitelist.IsWhitelistPassOrNull(ent.Comp.SpecialWeaponWhitelist, args.Used);
    }

    private void OnMobStateChanged(Entity<SpawnLootOnDeathComponent> ent, ref MobStateChangedEvent args)
    {
        if (!_mobState.IsDead(ent))
            return;

        var coordinates = Transform(ent).Coordinates;
        var special = ent.Comp.DoSpecialLoot && ent.Comp.SpecialTable != null;
        if (special)
            Spawn(ent.Comp.SpecialTable!);
        if (!special || ent.Comp.DropBoth)
            Spawn(ent.Comp.Table);
        if (ent.Comp.DeleteOnDeath)
            QueueDel(ent);

        void Spawn(Content.Shared.EntityTable.EntitySelectors.EntityTableSelector? table)
        {
            if (table == null)
                return;
            foreach (var prototype in _entityTable.GetSpawns(table))
                base.Spawn(prototype, coordinates);
        }
    }
}
