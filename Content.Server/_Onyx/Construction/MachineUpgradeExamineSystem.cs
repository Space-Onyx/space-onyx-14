// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Server.Construction.Components;
using Content.Shared._Onyx.Construction;
using Content.Shared.Examine;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Onyx.Construction;

public sealed partial class MachineUpgradeExamineSystem : EntitySystem
{
    [Dependency] private ExamineSystemShared _examine = default!;

    private static readonly SpriteSpecifier UpgradeIcon = new SpriteSpecifier.Rsi(
        new ResPath("/Textures/Objects/Misc/stock_parts.rsi"),
        "micro_mani");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MachineComponent, GetVerbsEvent<ExamineVerb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<MachineComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var message = new FormattedMessage();
        RaiseLocalEvent(ent, new MachineUpgradeExamineEvent(message));
        if (message.IsEmpty)
            return;

        var user = args.User;
        args.Verbs.Add(new ExamineVerb
        {
            Text = Loc.GetString("machine-upgrade-examinable-verb-text"),
            Message = Loc.GetString("machine-upgrade-examinable-verb-message"),
            Category = VerbCategory.Examine,
            Icon = UpgradeIcon,
            Act = () => _examine.SendExamineTooltip(user, ent, message, false, false),
        });
    }
}
