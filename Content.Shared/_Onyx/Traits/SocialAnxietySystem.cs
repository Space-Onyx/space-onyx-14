using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;

namespace Content.Shared._Onyx.Traits;

public sealed partial class SocialAnxietySystem : EntitySystem
{
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SocialAnxietyComponent, InteractionSuccessEvent>(OnHug);
    }

    private void OnHug(Entity<SocialAnxietyComponent> ent, ref InteractionSuccessEvent args)
    {
        _standing.Down(ent);
        _stun.TryUpdateStunDuration(ent, TimeSpan.FromSeconds(ent.Comp.DownedTime));
        _popup.PopupPredicted(Loc.GetString("social-anxiety-hugged", ("user", Identity.Name(ent, EntityManager))), ent, ent, PopupType.MediumCaution);
    }
}
