// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Server.Hands.Systems;
using Content.Shared.Genetics;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction.Components;

namespace Content.Server.Genetics.System;

public sealed partial class TelekinesisGenSystem : EntitySystem
{
    [Dependency] private HandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TelekinesisGenComponent, ComponentStartup>(OnTelekinesisStartup);
        SubscribeLocalEvent<TelekinesisGenComponent, ComponentShutdown>(OnTelekinesisShutdown);
    }

    private void OnTelekinesisStartup(EntityUid uid, TelekinesisGenComponent component, ComponentStartup args)
    {
        if (!HasComp<HandsComponent>(uid))
            return;

        _hands.AddHand(uid, component.HandId, component.HandPos);

        var coords = Transform(uid).Coordinates;
        var item = Spawn(component.ItemPrototype, coords);
        component.TelekinesisItem = item;

        if (_hands.TryPickup(uid, item, component.HandId, checkActionBlocker: false))
            EnsureComp<UnremoveableComponent>(item);
    }

    private void OnTelekinesisShutdown(Entity<TelekinesisGenComponent> entity, ref ComponentShutdown args)
    {
        if (entity.Comp.TelekinesisItem is { Valid: true } item)
            QueueDel(item);

        _hands.RemoveHand(entity.Owner, entity.Comp.HandId);
    }
}
