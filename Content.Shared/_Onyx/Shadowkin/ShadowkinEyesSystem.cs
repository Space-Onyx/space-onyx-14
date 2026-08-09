using Content.Shared.Body;
using Content.Shared.Flash;
using Content.Shared.Overlays;

namespace Content.Shared._Onyx.Shadowkin;

public sealed partial class ShadowkinEyesSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<ShadowkinEyesComponent, OrganGotInsertedEvent>(OnInserted);
        SubscribeLocalEvent<ShadowkinEyesComponent, OrganGotRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<ShadowkinFlashVulnerableComponent, FlashAttemptEvent>(OnFlashAttempt);
    }

    private void OnInserted(Entity<ShadowkinEyesComponent> ent, ref OrganGotInsertedEvent args)
    {
        ent.Comp.GrantedNightVision = !HasComp<NightVisionComponent>(args.Target);
        if (ent.Comp.GrantedNightVision)
            AddComp<NightVisionComponent>(args.Target);

        ent.Comp.GrantedFlashVulnerability = !HasComp<ShadowkinFlashVulnerableComponent>(args.Target);
        if (ent.Comp.GrantedFlashVulnerability)
            AddComp<ShadowkinFlashVulnerableComponent>(args.Target);

        Dirty(ent);
    }

    private void OnRemoved(Entity<ShadowkinEyesComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (ent.Comp.GrantedNightVision)
            RemComp<NightVisionComponent>(args.Target);

        if (ent.Comp.GrantedFlashVulnerability)
            RemComp<ShadowkinFlashVulnerableComponent>(args.Target);
    }

    private static void OnFlashAttempt(Entity<ShadowkinFlashVulnerableComponent> ent, ref FlashAttemptEvent args)
    {
        args.Cancelled = false;
    }
}
