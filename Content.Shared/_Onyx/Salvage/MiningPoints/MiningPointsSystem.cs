using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared._Onyx.Materials;
using Content.Shared.Lathe;
using Content.Shared.Materials.OreSilo;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Onyx.Salvage.MiningPoints;

public sealed partial class MiningPointsSystem : EntitySystem
{
    private const long MaximumHalfUnits = (long) int.MaxValue * 2 + 1;
    private static readonly SoundSpecifier ClaimSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<SalvageMiningPointProcessorComponent>(LatheUiKey.Key, subs =>
        {
            subs.Event<ClaimMiningPointsMessage>(OnClaimMiningPoints);
        });
    }

    public void AddHalfUnits(Entity<MiningPointsComponent> entity, long amount)
    {
        if (amount <= 0)
            return;

        var current = Math.Clamp(entity.Comp.HalfUnits, 0, MaximumHalfUnits);
        var updated = current + Math.Min(amount, MaximumHalfUnits - current);
        if (updated == entity.Comp.HalfUnits)
            return;

        entity.Comp.HalfUnits = updated;
        Dirty(entity);
        var ev = new MiningPointsChangedEvent();
        RaiseLocalEvent(entity, ref ev);
    }

    public bool TryFindIdCard(EntityUid user, out Entity<MiningPointsComponent> card)
    {
        if (_idCard.TryFindIdCard(user, out Entity<IdCardComponent> idCard) &&
            TryComp<MiningPointsComponent>(idCard, out var points))
        {
            card = (idCard.Owner, points);
            return true;
        }

        card = default;
        return false;
    }

    public bool TrySpend(Entity<MiningPointsComponent> entity, int amount)
    {
        entity.Comp.HalfUnits = Math.Clamp(entity.Comp.HalfUnits, 0, MaximumHalfUnits);
        if (amount <= 0 || entity.Comp.Points < amount)
            return false;

        entity.Comp.HalfUnits -= amount * 2L;
        Dirty(entity);
        var ev = new MiningPointsChangedEvent();
        RaiseLocalEvent(entity, ref ev);
        return true;
    }

    public bool Transfer(Entity<MiningPointsComponent> source, Entity<MiningPointsComponent> destination, int amount)
    {
        if (amount <= 0 || destination.Comp.Points > int.MaxValue - amount || !TrySpend(source, amount))
            return false;

        AddHalfUnits(destination, amount * 2L);
        return true;
    }

    public bool IsProcessorSiloConnected(EntityUid processor)
    {
        return TryComp<OreSiloClientComponent>(processor, out var client) &&
               client.Silo is { } silo &&
               Exists(silo) &&
               Transform(processor).MapID == Transform(silo).MapID;
    }

    private void OnClaimMiningPoints(Entity<SalvageMiningPointProcessorComponent> entity,
        ref ClaimMiningPointsMessage args)
    {
        if (!args.Actor.Valid || TerminatingOrDeleted(args.Actor) || args.Amount is 0 or > int.MaxValue ||
            !IsProcessorSiloConnected(entity) ||
            !TryComp<MiningPointsComponent>(entity, out var source) ||
            !TryFindIdCard(args.Actor, out var card) ||
            !Transfer((entity.Owner, source), card, (int) args.Amount))
        {
            return;
        }

        _audio.PlayPredicted(ClaimSound, entity, args.Actor);
    }
}
