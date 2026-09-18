// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Text.RegularExpressions;
using Content.Server.Preferences.Managers;
using Content.Shared._Onyx.Ghost.Skins;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Ghost.Skins;

[AnyCommand]
public sealed partial class SetGhostSkinCommand : IConsoleCommand
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IServerPreferencesManager _prefs = default!;

    public string Command => "setghostskin";
    public string Description => Loc.GetString("setghostskin-command-description");
    public string Help => Loc.GetString("setghostskin-command-help-text");

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteLine(Loc.GetString("setghostskin-command-no-session"));
            return;
        }

        if (args.Length != 1)
        {
            shell.WriteLine(Help);
            return;
        }

        var skinId = args[0];

        if (!_proto.TryIndex<GhostSkinPrototype>(skinId, out var skin) || skin.Abstract)
        {
            shell.WriteLine(Loc.GetString("setghostskin-command-invalid-skin-id"));
            return;
        }

        if (!skin.CanUse(player, out var failReason, out _))
        {
            shell.WriteLine(StripMarkup(failReason.ToMarkup()));
            return;
        }

        await _prefs.SetGhostSkinAsync(player.UserId, skinId);
        shell.WriteLine(Loc.GetString("setghostskin-command-saved"));
    }

    private static string StripMarkup(string markup)
    {
        return MarkupTagRegex().Replace(markup, string.Empty);
    }

    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex MarkupTagRegex();
}
