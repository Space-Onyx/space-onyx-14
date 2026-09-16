// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.Construction;

/// <summary>
/// Predicts the machine upgrade examine verb on the client so the button
/// appears instantly with the examine tooltip. Details are still built and
/// shown by the server verb after the click.
/// </summary>
public sealed partial class MachineUpgradeExaminePredictSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private INetManager _net = default!;

    private const string PartsContainerId = "machine_parts";

    private static readonly SpriteSpecifier UpgradeIcon = new SpriteSpecifier.Rsi(
        new ResPath("/Textures/Objects/Misc/stock_parts.rsi"),
        "micro_mani");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GetVerbsEvent<ExamineVerb>>(OnGetVerbs);
    }

    private void OnGetVerbs(GetVerbsEvent<ExamineVerb> args)
    {
        if (!_net.IsClient)
            return;

        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!_containers.TryGetContainer(args.Target, PartsContainerId, out var container)
            || container.ContainedEntities.Count == 0)
            return;

        args.Verbs.Add(new ExamineVerb
        {
            Text = Loc.GetString("machine-upgrade-examinable-verb-text"),
            Message = Loc.GetString("machine-upgrade-examinable-verb-message"),
            Category = VerbCategory.Examine,
            Icon = UpgradeIcon,
        });
    }
}
