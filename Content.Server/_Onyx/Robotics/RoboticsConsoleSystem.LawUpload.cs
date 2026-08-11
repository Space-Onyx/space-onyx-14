using Content.Server.Silicons.Laws;
using Content.Server.Station.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
using Content.Shared.DeviceNetwork.Events;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Robotics;
using Content.Shared.Robotics.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.Laws.Components;
using Robust.Shared.Containers;

namespace Content.Server.Research.Systems;

public sealed partial class RoboticsConsoleSystem
{
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private SiliconLawSystem _laws = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private StationSystem _station = default!;

    private void InitializeLawUpload()
    {
        SubscribeLocalEvent<RoboticsConsoleComponent, EntInsertedIntoContainerMessage>(OnLawboardChanged);
        SubscribeLocalEvent<RoboticsConsoleComponent, EntRemovedFromContainerMessage>(OnLawboardChanged);
    }

    private void OnLawboardChanged(Entity<RoboticsConsoleComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        UpdateUserInterface(ent);
    }

    private void OnLawboardChanged(Entity<RoboticsConsoleComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        UpdateUserInterface(ent);
    }

    private bool TrackLawUploadTarget(Entity<RoboticsConsoleComponent> console, DeviceNetworkPacketEvent args)
    {
        if (!console.Comp.AllowLawUpload)
            return true;

        var target = args.Sender;
        if (!IsValidTarget(console, target))
        {
            console.Comp.LawUploadTargets.Remove(args.SenderAddress);
            return false;
        }

        console.Comp.LawUploadTargets[args.SenderAddress] = target;
        return true;
    }

    private void OnChangeLaws(Entity<RoboticsConsoleComponent> ent, ref RoboticsConsoleChangeLawsMessage args)
    {
        if (!ent.Comp.AllowLawUpload ||
            _lock.IsLocked(ent.Owner) ||
            !_access.IsAllowed(args.Actor, ent) ||
            !ent.Comp.Cyborgs.ContainsKey(args.Address) ||
            !ent.Comp.LawUploadTargets.TryGetValue(args.Address, out var target) ||
            !IsValidTarget(ent, target) ||
            !TryComp<SiliconLawProviderComponent>(target, out var targetProvider) ||
            !_slots.TryGetSlot(ent, ent.Comp.LawboardSlot, out var slot) ||
            slot.Item is not { } board ||
            !HasComp<ItemComponent>(board) ||
            !TryComp<SiliconLawProviderComponent>(board, out var boardProvider))
        {
            return;
        }

        var lawset = _laws.CopyLawset((board, boardProvider), (target, targetProvider));
        _adminLogger.Add(
            LogType.SiliconLaw,
            LogImpact.High,
            $"{ToPrettyString(args.Actor):user} uploaded laws from {ToPrettyString(board)} to {ToPrettyString(target)} [{lawset.LoggingString()}]");

        var message = Loc.GetString(ent.Comp.ChangeLawsMessage, ("name", Name(target)));
        _radio.SendRadioMessage(ent, message, ent.Comp.RadioChannel, ent);
    }

    private bool IsValidTarget(Entity<RoboticsConsoleComponent> console, EntityUid target)
    {
        if (Deleted(target) ||
            !HasComp<BorgTransponderComponent>(target) ||
            !HasComp<BorgChassisComponent>(target) ||
            !HasComp<SiliconLawProviderComponent>(target) ||
            !TryComp<MobStateComponent>(target, out var mobState) ||
            _mobState.IsDead(target, mobState))
        {
            return false;
        }

        return _station.GetOwningStation(console) is { } station &&
               _station.GetOwningStation(target) == station;
    }

    private bool HasLawboard(Entity<RoboticsConsoleComponent> ent)
    {
        return ent.Comp.AllowLawUpload &&
               _slots.TryGetSlot(ent, ent.Comp.LawboardSlot, out var slot) &&
               slot.Item is { } board &&
               HasComp<SiliconLawProviderComponent>(board);
    }
}
