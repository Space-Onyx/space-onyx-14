// Content adapted from Goob-Station (https://github.com/Goob-Station/Goob-Station/pull/6988), licensed under AGPL-3.0-or-later.

using Content.Shared.Chat.Prototypes;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Intellicard;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Silicon.Intellicard;

public sealed partial class IntellicardableMindSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private NameModifierSystem _nameModifier = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedEyeSystem _eye = default!;

    private static readonly EntProtoId DefaultAi = "StationAiBrain";
    private static readonly ProtoId<ChatNotificationPrototype> DownloadNotification = "IntellicardDownload";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IntellicardComponent, AfterInteractEvent>(OnInteract);
        SubscribeLocalEvent<IntellicardableMindComponent, IntellicardableMindDoAfterEvent>(OnDoAfter);
    }

    private void OnInteract(Entity<IntellicardComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled ||
            !args.CanReach ||
            args.Target is not { } target ||
            !TryComp<IntellicardableMindComponent>(target, out var intellicardable) ||
            !TryComp<StationAiHolderComponent>(ent, out var holder))
            return;

        var targetMind = EnsureComp<MindContainerComponent>(target);
        var cardBrain = holder.Slot.Item;
        if (TryComp<MindContainerComponent>(cardBrain, out var cardMind) && cardMind.Mind == null)
        {
            _popup.PopupClient(Loc.GetString("intellicard-extras-contained-missing"), args.User, args.User, PopupType.MediumCaution);
            QueueDel(cardBrain);
            args.Handled = true;
            return;
        }

        var cardHasAi = _slots.CanEject(ent, holder.Slot, args.User);
        var targetHasAi = targetMind.Mind != null;
        if (cardHasAi == targetHasAi)
        {
            var message = cardHasAi ? "intellicard-extras-target-occupied" : "intellicard-extras-target-empty";
            _popup.PopupClient(Loc.GetString(message), args.User, args.User, PopupType.Medium);
            args.Handled = true;
            return;
        }

        if (targetHasAi)
        {
            var ev = new ChatNotificationEvent(DownloadNotification, ent, args.User);
            RaiseLocalEvent(target, ref ev);
        }

        var delay = cardHasAi
            ? ent.Comp.UploadTime * intellicardable.UploadTimeFactor
            : ent.Comp.DownloadTime * intellicardable.DownloadTimeFactor;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager,
            args.User,
            delay,
            new IntellicardableMindDoAfterEvent(),
            target,
            ent)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
            BreakOnDropItem = true,
        });
        args.Handled = true;
    }

    private void OnDoAfter(Entity<IntellicardableMindComponent> ent, ref IntellicardableMindDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } card || _net.IsClient ||
            !TryComp<StationAiHolderComponent>(card, out var holder) ||
            !TryComp<MindContainerComponent>(ent, out var targetMind))
            return;

        var cardBrain = holder.Slot.Item;
        TryComp<MindContainerComponent>(cardBrain, out var cardMind);
        var cardHasAi = _slots.CanEject(card, holder.Slot, args.User) && cardMind?.Mind != null;
        var targetHasAi = targetMind.Mind != null;

        if (cardHasAi && !targetHasAi)
        {
            _metaData.SetEntityName(ent, _nameModifier.GetBaseName(cardBrain!.Value));
            _mind.TransferTo(cardMind!.Mind!.Value, ent, ghostCheckOverride: true);
            _mind.UnVisit(cardMind.Mind.Value);
            QueueDel(cardBrain);
            _audio.PlayPvs(holder.Slot.InsertSound, ent);
            args.Handled = true;
            return;
        }

        if (!cardHasAi && targetHasAi)
        {
            var newCardBrain = SpawnInContainerOrDrop(DefaultAi, card, StationAiCoreComponent.Container);
            _metaData.SetEntityName(newCardBrain, _nameModifier.GetBaseName(ent.Owner));
            _mind.TransferTo(targetMind.Mind!.Value, newCardBrain, ghostCheckOverride: true);
            _mind.UnVisit(targetMind.Mind.Value);
            _audio.PlayPvs(holder.Slot.InsertSound, card);
            _eye.SetDrawFov(newCardBrain, true);
            args.Handled = true;
        }
    }
}

[Serializable, NetSerializable]
public sealed partial class IntellicardableMindDoAfterEvent : SimpleDoAfterEvent;
