using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.RCD.Systems;

public sealed partial class RCDSystem
{
    [Dependency] private SharedAtmosPipeLayersSystem _pipeLayers = default!;

    private void InitializePipeLayers()
    {
        SubscribeNetworkEvent<RCDConstructionGhostPipeLayerEvent>(OnRCDConstructionGhostPipeLayerEvent);
    }

    private void OnRCDConstructionGhostPipeLayerEvent(RCDConstructionGhostPipeLayerEvent ev, EntitySessionEventArgs session)
    {
        var uid = GetEntity(ev.NetEntity);
        if (session.SenderSession.AttachedEntity is not { } player || _hands.GetActiveItem(player) != uid ||
            !TryComp(uid, out RCDComponent? rcd) || !rcd.IsRpd)
            return;

        rcd.ConstructionPipeLayer = ev.Layer;
        Dirty(uid, rcd);
    }

    public void SetConstructionPipeLayer(Entity<RCDComponent> rcd, AtmosPipeLayer layer)
    {
        if (!rcd.Comp.IsRpd || rcd.Comp.ConstructionPipeLayer == layer)
            return;

        rcd.Comp.ConstructionPipeLayer = layer;
        Dirty(rcd);
        if (_net.IsClient)
            RaiseNetworkEvent(new RCDConstructionGhostPipeLayerEvent(GetNetEntity(rcd), layer));
    }

    public EntProtoId? GetConstructionPrototype(RCDPrototype prototype, RCDComponent component)
    {
        var selected = component.UseMirrorPrototype && !string.IsNullOrEmpty(prototype.MirrorPrototype)
            ? prototype.MirrorPrototype
            : prototype.Prototype;

        if (!component.IsRpd || !prototype.PipeLayers || selected == null ||
            !ProtoMan.TryIndex<EntityPrototype>(selected, out var entityPrototype) ||
            !entityPrototype.TryComp<AtmosPipeLayersComponent>(out var layers, EntityManager.ComponentFactory) ||
            !_pipeLayers.TryGetAlternativePrototype(layers, component.ConstructionPipeLayer, out var alternative))
            return selected;

        return alternative;
    }
}
