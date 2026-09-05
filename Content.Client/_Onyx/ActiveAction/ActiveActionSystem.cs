// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.ActiveAction;
using Content.Shared.Verbs;
using Robust.Client.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Onyx.ActiveAction;

public sealed partial class ActiveActionSystem : SharedActiveActionSystem
{
    [Dependency] private IPlayerManager _player = default!;

    private ActiveActionWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ActiveActionComponent, GetVerbsEvent<ExamineVerb>>(OnGetVerbs);
    }

    public override void Shutdown()
    {
        _window?.Close();
        base.Shutdown();
    }

    private void OnGetVerbs(Entity<ActiveActionComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!Enabled || args.User != ent.Owner || _player.LocalEntity != ent.Owner)
            return;

        args.Verbs.Add(new ExamineVerb
        {
            Text = Loc.GetString("active-action-verb"),
            Category = VerbCategory.Examine,
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/_Onyx/Interface/Chat/plus.png")),
            Act = () => OpenWindow(ent.Comp.Text),
            ClientExclusive = true,
        });
    }

    private void OpenWindow(string? text)
    {
        _window?.Close();
        _window = new ActiveActionWindow(text ?? string.Empty);
        _window.Submitted += RequestSetActiveAction;
        _window.OnClose += () => _window = null;
        _window.OpenCentered();
    }
}
