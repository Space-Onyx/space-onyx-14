using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared._Onyx.Flashbang.Components;

namespace Content.Shared._Onyx.Flashbang;

public sealed class SharedFlashbangSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FlashSoundSuppressionComponent, GetFlashbangedEvent>(OnFlashbanged);
        SubscribeLocalEvent<FlashSoundSuppressionComponent, InventoryRelayedEvent<GetFlashbangedEvent>>(OnInventoryFlashbanged);
        SubscribeLocalEvent<FlashSoundSuppressionComponent, ExaminedEvent>(OnExamined);
    }

    private static void OnFlashbanged(Entity<FlashSoundSuppressionComponent> entity, ref GetFlashbangedEvent args)
    {
        args.ProtectionRange = MathF.Min(args.ProtectionRange, entity.Comp.ProtectionRange);
    }

    private static void OnInventoryFlashbanged(Entity<FlashSoundSuppressionComponent> entity,
        ref InventoryRelayedEvent<GetFlashbangedEvent> args)
    {
        args.Args.ProtectionRange = MathF.Min(args.Args.ProtectionRange, entity.Comp.ProtectionRange);
    }

    private void OnExamined(Entity<FlashSoundSuppressionComponent> entity, ref ExaminedEvent args)
    {
        var message = entity.Comp.ProtectionRange > 0
            ? Loc.GetString("flash-sound-suppression-examine", ("range", entity.Comp.ProtectionRange))
            : Loc.GetString("flash-sound-suppression-fully-examine");
        args.PushMarkup(message);
    }
}
