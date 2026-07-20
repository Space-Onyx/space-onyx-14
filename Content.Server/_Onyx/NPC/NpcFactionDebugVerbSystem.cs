using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.NPC.Components;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Player;
using Content.Server._Onyx.NPC.UI;

namespace Content.Server._Onyx.NPC;

public sealed partial class NpcFactionDebugVerbSystem : EntitySystem
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private EuiManager _eui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NpcFactionMemberComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<NpcFactionMemberComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!_admin.HasAdminFlag(args.User, AdminFlags.Debug))
            return;

        if (!TryComp<ActorComponent>(args.User, out var actor) || actor == null)
            return;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("npc-faction-editor-verb"),
            Category = VerbCategory.Debug,
            Act = () => _eui.OpenEui(new NpcFactionEditorEui(ent.Owner), actor.PlayerSession),
        });
    }
}
