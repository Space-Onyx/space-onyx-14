using Content.Shared._Onyx.Bitrunning.Components;
using Content.Shared._Onyx.Bitrunning.Systems;
using Content.Shared._Onyx.Salvage.MiningPoints;
using Content.Shared.PDA;

namespace Content.Server.PDA;

public sealed partial class PdaSystem
{
    private void InitializeMiningPoints()
    {
        SubscribeLocalEvent<MiningPointsComponent, MiningPointsChangedEvent>(OnMiningPointsChanged);
        SubscribeLocalEvent<BitrunningPointsComponent, BitrunningPointsChangedEvent>(OnBitrunningPointsChanged);
    }

    private void OnMiningPointsChanged(Entity<MiningPointsComponent> card, ref MiningPointsChangedEvent args)
    {
        var parent = Transform(card).ParentUid;
        if (TryComp<PdaComponent>(parent, out var pda) && pda.ContainedId == card.Owner)
            UpdatePdaUi(parent, pda);
    }

    private void OnBitrunningPointsChanged(Entity<BitrunningPointsComponent> card, ref BitrunningPointsChangedEvent args)
    {
        var parent = Transform(card).ParentUid;
        if (TryComp<PdaComponent>(parent, out var pda) && pda.ContainedId == card.Owner)
            UpdatePdaUi(parent, pda);
    }
}
