// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.Clothing;
using Content.Shared.Inventory;

namespace Content.Server._Onyx.Mood;

public sealed partial class DirtyClothingMoodSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MoodSystem _mood = default!;

    private const float CheckInterval = 2f;
    private const string SocksSlot = "socks";
    private const string UnderwearBottomSlot = "underwearb";
    private const string UnderwearTopSlot = "underweart";
    private const string UniformSlot = "jumpsuit";

    private EntityQuery<ClothingDirtableComponent> _dirtableQuery;

    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();
        _dirtableQuery = GetEntityQuery<ClothingDirtableComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < CheckInterval)
            return;

        _accumulator = 0f;

        var query = EntityQueryEnumerator<MoodComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            Sync(uid, "DirtySocks", IsSlotDirty(uid, SocksSlot));
            Sync(uid, "DirtyUnderwear", IsSlotDirty(uid, UnderwearBottomSlot) || IsSlotDirty(uid, UnderwearTopSlot));
            Sync(uid, "DirtyUniform", IsSlotDirty(uid, UniformSlot));
        }
    }

    private void Sync(EntityUid uid, string effectId, bool dirty)
    {
        if (dirty)
            _mood.AddEffect(uid, effectId);
        else
            _mood.RemoveEffect(uid, effectId);
    }

    private bool IsSlotDirty(EntityUid wearer, string slot)
    {
        return _inventory.TryGetSlotEntity(wearer, slot, out var item)
            && item.HasValue
            && _dirtableQuery.TryComp(item.Value, out var dirtable)
            && dirtable.DirtColor != null;
    }
}
