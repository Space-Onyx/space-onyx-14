// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Paper;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared._Onyx.Paper;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Onyx.Contract;

public sealed partial class ContractSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId == null || !_prototype.TryIndex<JobPrototype>(args.JobId, out var job))
            return;

        if (job.Contracts == null)
            return;

        if (!_inventory.TryGetSlotEntity(args.Mob, "back", out var backUid)
            || !TryComp<StorageComponent>(backUid, out var storage))
            return;

        foreach (var contract in job.Contracts)
        {
            var spawned = Spawn(contract);
            if (!TryComp<PaperComponent>(spawned, out var paper))
            {
                QueueDel(spawned);
                continue;
            }

            var identity = EnsureComp<SignatureIdentityComponent>(args.Mob);
            if (string.IsNullOrEmpty(identity.HandwritingId))
            {
                identity.HandwritingId = Guid.NewGuid().ToString("N");
                Dirty(args.Mob, identity);
            }

            paper.SignedBy.Add(new SignatureDisplayInfo
            {
                SignedName = MetaData(args.Mob).EntityName,
                FontId = "Sign",
                FontSize = 16,
                SignColor = Color.DarkSlateGray,
                HandwritingId = identity.HandwritingId,
            });
            Dirty(spawned, paper);

            _paper.TryStamp((spawned, paper),
                new StampDisplayInfo
                {
                    StampedName = Loc.GetString("contract-stamp-legal-department"),
                    StampedColor = Color.DarkGreen,
                },
                "paper_stamp-centcom");

            _metadata.SetEntityName(spawned, Loc.GetString("contract-paper-name", ("number", _random.Next(1000000, 9999999))));
            _metadata.SetEntityDescription(spawned, Loc.GetString("contract-paper-description"));

            _storage.Insert(backUid.Value, spawned, out _, storageComp: storage, playSound: false);
        }
    }
}
