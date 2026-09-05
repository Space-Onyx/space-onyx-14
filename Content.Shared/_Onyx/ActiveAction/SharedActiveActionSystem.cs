// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.CCVar;
using Content.Shared.Examine;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.ActiveAction;

public abstract partial class SharedActiveActionSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private INetManager _net = default!;

    public const int MaxLength = 128;

    protected bool Enabled;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_configuration, CCVars.ActiveActionEnabled, value => Enabled = value, true);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<ActiveActionComponent, ExaminedEvent>(OnExamined);
        SubscribeNetworkEvent<SetActiveActionEvent>(OnSetActiveAction);
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        if (_net.IsServer)
            EnsureComp<ActiveActionComponent>(args.Entity);
    }

    private void OnExamined(Entity<ActiveActionComponent> ent, ref ExaminedEvent args)
    {
        if (!Enabled || string.IsNullOrEmpty(ent.Comp.Text))
            return;

        args.PushMarkup(Loc.GetString("active-action-examine", ("action", FormattedMessage.EscapeText(ent.Comp.Text))), 100);
    }

    private void OnSetActiveAction(SetActiveActionEvent args, EntitySessionEventArgs session)
    {
        if (session.SenderSession.AttachedEntity is not { } uid)
            return;

        TrySetActiveAction(uid, args.Text);
    }

    public bool TrySetActiveAction(Entity<ActiveActionComponent?> ent, string text)
    {
        if (!CanSetActiveAction(text))
            return false;

        var component = EnsureComp<ActiveActionComponent>(ent);
        component.Text = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        Dirty(ent.Owner, component);
        return true;
    }

    public bool CanSetActiveAction(string text)
    {
        return _net.IsServer && Enabled && text.Length <= MaxLength;
    }

    public void RequestSetActiveAction(string text)
    {
        if (_net.IsClient && Enabled && text.Length <= MaxLength)
            RaiseNetworkEvent(new SetActiveActionEvent(text));
    }
}
