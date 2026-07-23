using Content.Shared._Onyx.Paper;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Onyx.Paper;

public sealed partial class TicketMachineSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TicketMachineComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<TicketMachineComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInteractHand(Entity<TicketMachineComponent> ent, ref InteractHandEvent args)
    {
        if (!TryComp(ent, out UseDelayComponent? useDelay) || _useDelay.IsDelayed((ent, useDelay)))
        {
            _popup.PopupEntity(Loc.GetString("paper-component-ticket-failed"), args.User, args.User, PopupType.Small);
            return;
        }

        var number = (ent.Comp.Queue + 1).ToString();
        var ticket = Spawn(ent.Comp.TicketPrototype, Transform(ent).Coordinates);
        if (!TryComp(ticket, out PaperComponent? paper))
        {
            QueueDel(ticket);
            return;
        }

        var stamp = new StampDisplayInfo
        {
            StampedName = number,
            StampedColor = Color.FromHex("#333333"),
        };
        _paper.TryStamp((ticket, paper), stamp, "paper_stamp-generic");
        _paper.SetContent((ticket, paper), Loc.GetString("paper-component-ticket", ("queue", number)));
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/short_print_and_rip.ogg"), ent);

        ent.Comp.Queue++;
        Dirty(ent);
        _hands.TryPickupAnyHand(args.User, ticket);
        _useDelay.TryResetDelay((ent, useDelay));
    }

    private void OnExamined(Entity<TicketMachineComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.AddMarkup(Loc.GetString("paper-component-ticket-count", ("number", ent.Comp.Queue + 1)));
    }
}
