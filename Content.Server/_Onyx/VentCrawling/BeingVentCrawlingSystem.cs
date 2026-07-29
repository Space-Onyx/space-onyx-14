// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2025 Fishbait <Fishbait@git.ml>
// SPDX-FileCopyrightText: 2025 Rinary <72972221+Rinary1@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 fishbait <gnesse@gmail.com>
// SPDX-FileCopyrightText: 2025 ss14-Starlight <ss14-Starlight@outlook.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Systems;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared._Onyx.VentCrawling;
using Content.Shared.Actions.Events;
using Content.Shared.Atmos;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.NodeContainer;
using Content.Shared.Throwing;

namespace Content.Server._Onyx.VentCrawling;

public sealed partial class BeingVentCrawSystem : EntitySystem
{
    [Dependency] private NodeContainerSystem _nodeContainer = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BeingVentCrawlerComponent, InhaleLocationEvent>(OnInhaleLocation);
        SubscribeLocalEvent<BeingVentCrawlerComponent, ExhaleLocationEvent>(OnExhaleLocation);
        SubscribeLocalEvent<BeingVentCrawlerComponent, AtmosExposedGetAirEvent>(OnGetAir);
        SubscribeLocalEvent<BeingVentCrawlerComponent, ActionAttemptEvent>(OnActionAttempt);
        SubscribeLocalEvent<BeingVentCrawlerComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<BeingVentCrawlerComponent, PickupAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<BeingVentCrawlerComponent, ThrowAttemptEvent>(OnThrowAttempt);
        SubscribeLocalEvent<BeingVentCrawlerComponent, DropAttemptEvent>(OnDropAttempt);
        SubscribeLocalEvent<BeingVentCrawlerComponent, IsUnequippingAttemptEvent>(OnUnequipAttempt);
        SubscribeLocalEvent<BeingVentCrawlerComponent, IsEquippingAttemptEvent>(OnEquipAttempt);
    }

    private bool TryGetPipe(BeingVentCrawlerComponent component, out PipeNode? pipe)
    {
        pipe = null;
        if (!TryComp<VentCrawlerHolderComponent>(component.Holder, out var holder)
            || holder.CurrentTube == null
            || !TryComp<NodeContainerComponent>(holder.CurrentTube.Value, out var container))
            return false;

        foreach (var node in container.Nodes)
        {
            if (!_nodeContainer.TryGetNode(container, node.Key, out PipeNode? found))
                continue;

            pipe = found;
            return true;
        }

        return false;
    }

    private void OnGetAir(EntityUid uid, BeingVentCrawlerComponent component, ref AtmosExposedGetAirEvent args)
    {
        if (!TryGetPipe(component, out var pipe) || pipe is null)
            return;
        args.Gas = pipe.Air;
        args.Handled = true;
    }

    private void OnInhaleLocation(EntityUid uid, BeingVentCrawlerComponent component, InhaleLocationEvent args)
    {
        if (TryGetPipe(component, out var pipe) && pipe is not null)
            args.Gas = pipe.Air;
    }

    private void OnExhaleLocation(EntityUid uid, BeingVentCrawlerComponent component, ExhaleLocationEvent args)
    {
        if (TryGetPipe(component, out var pipe) && pipe is not null)
            args.Gas = pipe.Air;
    }

    private void OnActionAttempt(EntityUid uid, BeingVentCrawlerComponent component, ref ActionAttemptEvent args) => args.Cancelled = true;
    private void OnAttackAttempt(EntityUid uid, BeingVentCrawlerComponent component, ref AttackAttemptEvent args) => args.Cancel();
    private void OnPickupAttempt(EntityUid uid, BeingVentCrawlerComponent component, ref PickupAttemptEvent args) => args.Cancel();
    private void OnThrowAttempt(EntityUid uid, BeingVentCrawlerComponent component, ref ThrowAttemptEvent args) => args.Cancel();
    private void OnDropAttempt(EntityUid uid, BeingVentCrawlerComponent component, ref DropAttemptEvent args) => args.Cancel();
    private void OnUnequipAttempt(EntityUid uid, BeingVentCrawlerComponent component, ref IsUnequippingAttemptEvent args) => args.Cancel();
    private void OnEquipAttempt(EntityUid uid, BeingVentCrawlerComponent component, ref IsEquippingAttemptEvent args) => args.Cancel();
}
