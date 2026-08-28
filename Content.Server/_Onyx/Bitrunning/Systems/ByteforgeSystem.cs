using Content.Shared._Onyx.Effects;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Stacks;
using Content.Shared._Onyx.Bitrunning;
using Content.Shared._Onyx.Bitrunning.Components;
using Content.Shared._Onyx.Bitrunning.Prototypes;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Paper;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
namespace Content.Server._Onyx.Bitrunning.Systems;

public sealed partial class ByteforgeSystem : EntitySystem
{
    [Dependency] private BitrunningDomainSystem _domains = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private StorageSystem _storage = default!;
    [Dependency] private EntityStorageSystem _entityStorage = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private SparksSystem _sparks = default!;
    [Dependency] private SharedStackSystem _stack = default!;

    private const string ServerSourcePort = "BitrunningServerSource";
    private const string ByteforgeSinkPort = "BitrunningByteforgeSink";

    public override void Initialize()
    {
        SubscribeLocalEvent<ByteforgeComponent, MapInitEvent>(OnByteforgeMapInit);
        SubscribeLocalEvent<ByteforgeComponent, PowerChangedEvent>(OnByteforgePowerChanged);
        SubscribeLocalEvent<QuantumServerComponent, NewLinkEvent>(OnServerNewLink);
        SubscribeLocalEvent<QuantumServerComponent, PortDisconnectedEvent>(OnServerPortDisconnected);
        SubscribeLocalEvent<QuantumServerComponent, GotEmaggedEvent>(OnServerEmagged);
    }

    private void OnByteforgeMapInit(Entity<ByteforgeComponent> ent, ref MapInitEvent args)
    {
        _appearance.SetData(ent, ByteforgeVisuals.ByteforgePowered, _power.IsPowered(ent.Owner));
        _appearance.SetData(ent, ByteforgeVisuals.ByteforgeActive, false);
        _appearance.SetData(ent, ByteforgeVisuals.ByteforgeAngry, IsLinkedServerEmagged(ent.Comp));
    }

    private void OnByteforgePowerChanged(Entity<ByteforgeComponent> ent, ref PowerChangedEvent args)
    {
        _appearance.SetData(ent, ByteforgeVisuals.ByteforgePowered, args.Powered);
    }

    private void OnServerEmagged(Entity<QuantumServerComponent> ent, ref GotEmaggedEvent args)
    {
        args.Handled = true;
        UpdateByteforgeEmagVisual(ent.Comp);
    }

    private void OnServerNewLink(Entity<QuantumServerComponent> ent, ref NewLinkEvent args)
    {
        if (args.Source != ent.Owner || args.SourcePort != ServerSourcePort || args.SinkPort != ByteforgeSinkPort)
            return;

        if (!TryComp<ByteforgeComponent>(args.Sink, out var byteforge))
            return;

        if (ent.Comp.LinkedByteforge is { } oldByteforge && oldByteforge != args.Sink && TryComp<ByteforgeComponent>(oldByteforge, out var oldByteforgeComp))
        {
            oldByteforgeComp.LinkedServer = null;
            _appearance.SetData(oldByteforge, ByteforgeVisuals.ByteforgeAngry, false);
        }

        ent.Comp.LinkedByteforge = args.Sink;
        byteforge.LinkedServer = ent.Owner;
        UpdateByteforgeEmagVisual(ent.Comp);
        Dirty(ent);
    }

    private void OnServerPortDisconnected(Entity<QuantumServerComponent> ent, ref PortDisconnectedEvent args)
    {
        if (args.Port != ServerSourcePort)
            return;

        if (ent.Comp.LinkedByteforge is { } linked && TryComp<ByteforgeComponent>(linked, out var byteforge))
            byteforge.LinkedServer = null;

        if (ent.Comp.LinkedByteforge is { } oldLinked && Exists(oldLinked))
            _appearance.SetData(oldLinked, ByteforgeVisuals.ByteforgeAngry, false);

        ent.Comp.LinkedByteforge = null;
        Dirty(ent);
    }

    public bool HasLinkedByteforge(EntityUid serverUid, QuantumServerComponent server)
    {
        if (server.LinkedByteforge is not { } byteforgeUid || !Exists(byteforgeUid))
            return false;

        return TryComp<ByteforgeComponent>(byteforgeUid, out var byteforge) && byteforge.LinkedServer == serverUid;
    }

    public bool TryDeliverObjectiveCargoToByteforge(EntityUid serverUid, EntityUid cargoUid, float rewardsMultiplier)
    {
        if (!TryComp<QuantumServerComponent>(serverUid, out var server))
            return false;

        if (HasComp<BitrunningDeliveredObjectiveCargoComponent>(cargoUid))
            return false;

        if (!HasLinkedByteforge(serverUid, server))
            return false;

        var byteforgeUid = server.LinkedByteforge!.Value;
        if (!TryComp(byteforgeUid, out TransformComponent? byteforgeXform))
            return false;

        if (!_prototype.HasIndex<EntityPrototype>(server.RewardCachePrototype))
        {
            Log.Warning($"Invalid reward cache prototype '{server.RewardCachePrototype}' on server {ToPrettyString(serverUid)}.");
            return false;
        }

        var rewardCargoUid = Spawn(server.RewardCachePrototype, byteforgeXform.Coordinates);
        _sparks.DoSparks(byteforgeXform.Coordinates);

        if (!TryFillRewardCache(rewardCargoUid, server, rewardsMultiplier))
        {
            Log.Warning($"Failed to fill delivered cargo reward crate for server {ToPrettyString(serverUid)}.");
            QueueDel(rewardCargoUid);
            return false;
        }

        EnsureComp<BitrunningDeliveredObjectiveCargoComponent>(cargoUid);
        PulseByteforge(byteforgeUid);
        QueueDel(cargoUid);
        return true;
    }

    private void PulseByteforge(EntityUid byteforgeUid)
    {
        if (!TryComp<ByteforgeComponent>(byteforgeUid, out var byteforge))
            return;

        byteforge.VisualPulseSerial++;
        var pulseSerial = byteforge.VisualPulseSerial;

        _appearance.SetData(byteforgeUid, ByteforgeVisuals.ByteforgeAngry, IsLinkedServerEmagged(byteforge));
        _appearance.SetData(byteforgeUid, ByteforgeVisuals.ByteforgeActive, true);

        Timer.Spawn(TimeSpan.FromSeconds(1.4f),
            () =>
        {
            if (!TryComp<ByteforgeComponent>(byteforgeUid, out var byteforgeComp) || byteforgeComp.VisualPulseSerial != pulseSerial)
                return;

            _appearance.SetData(byteforgeUid, ByteforgeVisuals.ByteforgeActive, false);
        });
    }

    private bool IsLinkedServerEmagged(ByteforgeComponent byteforge)
    {
        return byteforge.LinkedServer is { } serverUid && HasComp<EmaggedComponent>(serverUid);
    }

    private void UpdateByteforgeEmagVisual(QuantumServerComponent server)
    {
        if (server.LinkedByteforge is not { } byteforgeUid || !Exists(byteforgeUid) || !TryComp<ByteforgeComponent>(byteforgeUid, out var byteforge))
            return;

        _appearance.SetData(byteforgeUid, ByteforgeVisuals.ByteforgeAngry, IsLinkedServerEmagged(byteforge));
    }

    public void RefreshLinkedByteforge(Entity<QuantumServerComponent> ent)
    {
        if (ent.Comp.LinkedByteforge is { } old && TryComp<ByteforgeComponent>(old, out var oldByteforge) && oldByteforge.LinkedServer == ent.Owner)
            oldByteforge.LinkedServer = null;

        ent.Comp.LinkedByteforge = null;

        if (!TryComp<DeviceLinkSourceComponent>(ent.Owner, out var source))
            return;

        foreach (var outputs in source.Outputs.Values)
        {
            foreach (var linkedEntity in outputs)
            {
                if (!TryComp<ByteforgeComponent>(linkedEntity, out var byteforge))
                    continue;

                ent.Comp.LinkedByteforge = linkedEntity;
                byteforge.LinkedServer = ent.Owner;
                UpdateByteforgeEmagVisual(ent.Comp);
                return;
            }
        }
    }

    public bool TryFillRewardCache(EntityUid cargoUid, QuantumServerComponent server, float rewardsMultiplier)
    {
        if (server.CurrentDomain == null ||
            !_domains.TryGetDomain(server.CurrentDomain.Value.Id, out var domain))
            return false;

        var coordinates = Transform(cargoUid).Coordinates;
        var insertedAny = false;
        foreach (var (prototypeId, amount) in domain.CompletionLoot)
        {
            for (var i = 0; i < amount; i++)
                insertedAny |= TryInsertReward(cargoUid, Spawn(prototypeId, coordinates));
        }

        var completionTime = _timing.CurTime - server.DomainStartTime;
        var grade = GradeCompletion(server, domain.Difficulty, domain.LootRewardPoints, completionTime);
        insertedAny |= TryInsertCertificate(cargoUid, coordinates, server, domain, completionTime, grade, rewardsMultiplier);

        if (domain.Difficulty >= BitrunningDifficulty.Medium &&
            grade is BitrunningCompletionGrade.A or BitrunningCompletionGrade.S &&
            !server.TechnologyDiskRewardSpawned)
        {
            server.TechnologyDiskRewardSpawned = true;
            insertedAny |= TryInsertReward(cargoUid, Spawn("TechnologyDiskBitrunningReward", coordinates));
            insertedAny |= TryInsertReward(cargoUid,
                Spawn(grade == BitrunningCompletionGrade.S
                    ? "ResearchDiskBitrunningExperimental1000"
                    : "ResearchDiskBitrunningExperimental500", coordinates));
        }

        insertedAny |= TryInsertOre(cargoUid, coordinates, "SteelOre1Unprocessed", domain.LootRewardPoints, rewardsMultiplier, 3f);
        insertedAny |= TryInsertOre(cargoUid, coordinates, "SpaceQuartz1Unprocessed", domain.LootRewardPoints, rewardsMultiplier, 2f);

        if (domain.LootRewardPoints > 1)
        {
            insertedAny |= TryInsertOre(cargoUid, coordinates, "SilverOre1Unprocessed", domain.LootRewardPoints, rewardsMultiplier, 0.7f);
        }

        if (domain.LootRewardPoints > 2)
        {
            insertedAny |= TryInsertOre(cargoUid, coordinates, "PlasmaOre1Unprocessed", domain.LootRewardPoints, rewardsMultiplier, 1f);
            insertedAny |= TryInsertOre(cargoUid, coordinates, "GoldOre1Unprocessed", domain.LootRewardPoints, rewardsMultiplier, 0.6f);
            insertedAny |= TryInsertOre(cargoUid, coordinates, "UraniumOre1Unprocessed", domain.LootRewardPoints, rewardsMultiplier, 0.4f);
        }

        if (domain.LootRewardPoints > 3)
        {
            insertedAny |= TryInsertOre(cargoUid, coordinates, "DiamondOre1Unprocessed", domain.LootRewardPoints, rewardsMultiplier, 0.3f);
            insertedAny |= TryInsertOre(cargoUid, coordinates, "MaterialBSCrystal1Unprocessed", domain.LootRewardPoints, rewardsMultiplier, 0.2f);
        }

        return insertedAny;
    }

    private bool TryInsertCertificate(
        EntityUid cargoUid,
        EntityCoordinates coordinates,
        QuantumServerComponent server,
        BitrunningVirtualDomainPrototype domain,
        TimeSpan completionTime,
        BitrunningCompletionGrade grade,
        float rewardsMultiplier)
    {
        var certificate = Spawn("PaperBitrunningCompletionCertificate", coordinates);
        if (!TryComp<PaperComponent>(certificate, out var paper))
        {
            QueueDel(certificate);
            return false;
        }

        _paper.SetContent((certificate, paper), Loc.GetString("bitrunning-completion-certificate-content",
            ("domain", Loc.GetString(domain.Name)),
            ("difficulty", Loc.GetString($"bitrunning-ui-difficulty-{domain.Difficulty.ToString().ToLowerInvariant()}")),
            ("threats", server.ThreatsSpawned),
            ("reward", domain.LootRewardPoints),
            ("multiplier", rewardsMultiplier.ToString("0.0")),
            ("time", completionTime.ToString(@"hh\:mm\:ss")),
            ("grade", grade.ToString()),
            ("randomized", server.WasRandomizedRun
                ? Loc.GetString("bitrunning-completion-certificate-randomized")
                : string.Empty)));
        _metaData.SetEntityName(certificate, Loc.GetString("bitrunning-completion-certificate-name"));
        return TryInsertReward(cargoUid, certificate);
    }

    private static BitrunningCompletionGrade GradeCompletion(
        QuantumServerComponent server,
        BitrunningDifficulty difficulty,
        int rewardPoints,
        TimeSpan completionTime)
    {
        var timeScore = completionTime.TotalMinutes switch
        {
            <= 1 => 10,
            <= 2 => 5,
            <= 5 => 3,
            <= 10 => 2,
            _ => 1,
        };

        var score = server.ThreatsSpawned * 5 +
                    rewardPoints +
                    timeScore * ((int) difficulty + 1);
        return score switch
        {
            <= 4 => BitrunningCompletionGrade.D,
            <= 7 => BitrunningCompletionGrade.C,
            <= 10 => BitrunningCompletionGrade.B,
            <= 13 => BitrunningCompletionGrade.A,
            _ => BitrunningCompletionGrade.S,
        };
    }

    private bool TryInsertOre(
        EntityUid cargoUid,
        EntityCoordinates coordinates,
        EntProtoId prototypeId,
        int rewardPoints,
        float rewardsMultiplier,
        float oreMultiplier)
    {
        var loot = Spawn(prototypeId, coordinates);
        var amount = Math.Max(1,
            (int) MathF.Ceiling(_random.NextFloat(0.5f, 1.5f) * (rewardPoints + rewardsMultiplier) * oreMultiplier));
        _stack.SetCount(loot, amount);
        return TryInsertReward(cargoUid, loot);
    }

    private bool TryInsertReward(EntityUid cargoUid, EntityUid loot)
    {
        if (TryComp<StorageComponent>(cargoUid, out var storage) &&
                _storage.Insert(cargoUid, loot, out _, storageComp: storage, playSound: false) ||
            TryComp<EntityStorageComponent>(cargoUid, out var entityStorage) &&
                _entityStorage.Insert(loot, cargoUid, entityStorage))
        {
            return true;
        }

        QueueDel(loot);
        return false;
    }

}
