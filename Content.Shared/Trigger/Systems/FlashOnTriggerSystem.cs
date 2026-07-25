using Content.Shared.Flash;
using Content.Shared._Onyx.Flashbang;
using Content.Shared.Trigger.Components.Effects;

namespace Content.Shared.Trigger.Systems;

public sealed partial class FlashOnTriggerSystem : XOnTriggerSystem<FlashOnTriggerComponent>
{
    [Dependency] private SharedFlashSystem _flash = default!;

    protected override void OnTrigger(Entity<FlashOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        _flash.FlashArea(target, args.User, ent.Comp.Range, ent.Comp.Duration, probability: ent.Comp.Probability,
            flashbang: ent.Comp.Flashbang);
        args.Handled = true;
    }
}
