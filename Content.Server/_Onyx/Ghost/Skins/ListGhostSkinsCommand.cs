// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Linq;
using System.Text;
using Content.Shared._Onyx.Ghost.Skins;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Ghost.Skins;

[AnyCommand]
public sealed partial class ListGhostSkinsCommand : IConsoleCommand
{
    [Dependency] private IPrototypeManager _proto = default!;

    public string Command => "listghostskins";
    public string Description => Loc.GetString("listghostskins-command-description");
    public string Help => Loc.GetString("listghostskins-command-help-text");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var skins = _proto.EnumeratePrototypes<GhostSkinPrototype>()
            .Where(s => !s.Abstract)
            .OrderBy(s => s.DisplayName);

        if (shell.Player is not { } player)
        {
            var sb = new StringBuilder();
            foreach (var skin in skins)
            {
                sb.AppendLine(skin.ID);
            }

            shell.WriteLine(sb.ToString());
            return;
        }

        if (args.Length > 1 || args.Length == 1 && args[0] != "all")
        {
            shell.WriteLine(Help);
            return;
        }

        var showAll = args.Length == 1;
        var result = new StringBuilder();
        result.AppendLine(Loc.GetString(showAll ? "listghostskins-all-skins" : "listghostskins-available-skins"));

        foreach (var skin in skins)
        {
            var available = skin.CanUse(player, out _, out var canSee);
            if (!canSee)
                continue;

            if (available)
                result.AppendLine($"- {skin.ID}");
            else if (showAll)
                result.AppendLine($"- {skin.ID} {Loc.GetString("listghostskins-locked")}");
        }

        shell.WriteLine(result.ToString());
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args) =>
        args.Length switch
        {
            1 => CompletionResult.FromHint("all"),
            _ => CompletionResult.Empty,
        };
}
