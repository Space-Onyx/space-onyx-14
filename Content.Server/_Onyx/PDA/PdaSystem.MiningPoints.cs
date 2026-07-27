using Content.Shared._Onyx.Salvage.MiningPoints;
using Content.Shared.PDA;

namespace Content.Server.PDA;

public sealed partial class PdaSystem
{
    private void InitializeMiningPoints()
    {
        SubscribeLocalEvent<MiningPointsComponent, MiningPointsChangedEvent>(OnMiningPointsChanged);
    }

    private void OnMiningPointsChanged(Entity<MiningPointsComponent> card, ref MiningPointsChangedEvent args)
    {
        var parent = Transform(card).ParentUid;
        if (TryComp<PdaComponent>(parent, out var pda) && pda.ContainedId == card.Owner)
            UpdatePdaUi(parent, pda);
    }
}
