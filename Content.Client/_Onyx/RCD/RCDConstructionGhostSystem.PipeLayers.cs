using Content.Shared.RCD.Systems;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client.RCD;

public sealed partial class RCDConstructionGhostSystem
{
    private EntProtoId? GetPipeLayerConstructionPrototype(RCDPrototype prototype, RCDComponent component)
        => EntitySystem.Get<RCDSystem>().GetConstructionPrototype(prototype, component);
}
