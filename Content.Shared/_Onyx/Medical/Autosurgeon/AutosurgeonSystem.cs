using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Forensics;
using Content.Shared.Inventory;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._Onyx.Medical.Autosurgeon;

public sealed partial class AutosurgeonSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AutosurgeonComponent, AutosurgeonDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<AutosurgeonComponent, DoAfterAttemptEvent<AutosurgeonDoAfterEvent>>(OnDoAfterAttempt);
        SubscribeLocalEvent<AutosurgeonComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<AutosurgeonComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
    }

    private void OnExamined(Entity<AutosurgeonComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.Used
            ? "autosurgeon-examine-used"
            : "autosurgeon-examine-ready"));
    }

    private void OnGetAlternativeVerbs(Entity<AutosurgeonComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("autosurgeon-verb-activate"),
            Disabled = ent.Comp.Used || ent.Comp.InUse ||
                !TryComp(ent, out StrapComponent? strap) || strap.BuckledEntities.Count != 1,
            Act = () => TryStartOperation(ent, user),
            Priority = 1,
        });
    }

    private bool TryStartOperation(Entity<AutosurgeonComponent> ent, EntityUid user)
    {
        if (ent.Comp.Used || ent.Comp.InUse ||
            !TryComp(ent, out StrapComponent? strap) || strap.BuckledEntities.Count != 1)
            return false;

        var patient = strap.BuckledEntities.First();

        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.Duration,
            new AutosurgeonDoAfterEvent(), ent, patient, ent)
        {
            NeedHand = true,
            BreakOnMove = true,
            MovementThreshold = 0.5f,
            AttemptFrequency = AttemptFrequency.EveryTick,
        };

        if (_net.IsClient || !_doAfter.TryStartDoAfter(doAfter))
            return false;

        ent.Comp.InUse = true;
        Dirty(ent);
        ent.Comp.ActiveSound = _audio.PlayPvs(ent.Comp.Sound, ent)?.Entity;
        return true;
    }

    private void OnDoAfterAttempt(Entity<AutosurgeonComponent> ent,
        ref DoAfterAttemptEvent<AutosurgeonDoAfterEvent> args)
    {
        if (ent.Comp.Used || args.DoAfter.Args.Target is not { } patient ||
            !TryComp(ent, out StrapComponent? strap) || strap.BuckledEntities.Count != 1 ||
            strap.BuckledEntities.First() != patient)
            args.Cancel();
    }

    private void OnDoAfter(Entity<AutosurgeonComponent> ent, ref AutosurgeonDoAfterEvent args)
    {
        _audio.Stop(ent.Comp.ActiveSound);
        ent.Comp.ActiveSound = null;

        if (_net.IsClient)
            return;

        ent.Comp.InUse = false;
        Dirty(ent);
        if (args.Cancelled || ent.Comp.Used || args.Target is not { } body ||
            !TryComp(ent, out StrapComponent? strap) || strap.BuckledEntities.Count != 1 ||
            strap.BuckledEntities.First() != body)
            return;

        ent.Comp.Used = true;
        Dirty(ent);

        var replacement = Spawn(ent.Comp.Replacement, Transform(body).Coordinates);
        if (ent.Comp.ChildReplacement is { } childPrototype)
        {
            var child = Spawn(childPrototype, Transform(body).Coordinates);
            if (!_body.TryAttachPart(replacement, child))
            {
                Del(child);
                Del(replacement);
                ent.Comp.Used = false;
                Dirty(ent);
                return;
            }
        }
        var success = ent.Comp.TargetOrgan is { } organSlot
            ? ReplaceOrgan(body, replacement, organSlot, ent.Comp.TargetPart, ent.Comp.TargetSymmetry)
            : ReplacePart(body, replacement, ent.Comp.TargetPart, ent.Comp.TargetSymmetry);

        if (!success)
        {
            Del(replacement);
            ent.Comp.Used = false;
            Dirty(ent);
            return;
        }

        _inventory.RefreshBodySlots(body);
    }

    private bool ReplacePart(EntityUid body, EntityUid replacement, BodyPartType type, BodyPartSymmetry symmetry)
    {
        if (!TryComp(replacement, out BodyPartComponent? replacementPart) ||
            replacementPart.PartType != type ||
            replacementPart.Symmetry != symmetry)
            return false;

        var oldPart = _body.GetBodyChildrenOfType(body, type)
            .FirstOrDefault(part => part.Component.Symmetry == symmetry);

        if (!oldPart.Id.Valid)
            return TryAttachToBody(body, replacement);

        if (oldPart.Component.Parent is not { } parent ||
            !_body.AreTransplantsCompatible(parent, replacement) ||
            !_body.TryDetachPart(oldPart.Id))
            return false;

        if (_body.TryAttachPart(parent, replacement))
            return true;

        _body.TryAttachPart(parent, oldPart.Id);
        return false;
    }

    private bool TryAttachToBody(EntityUid body, EntityUid replacement)
    {
        foreach (var (parent, _) in _body.GetBodyChildren(body))
        {
            if (_body.TryAttachPart(parent, replacement))
                return true;
        }

        return false;
    }

    private bool ReplaceOrgan(EntityUid body, EntityUid replacement, string slot,
        BodyPartType parentType, BodyPartSymmetry parentSymmetry)
    {
        if (string.IsNullOrWhiteSpace(slot) || !HasComp<OrganComponent>(replacement) ||
            HasComp<BodyPartComponent>(replacement))
            return false;

        var parent = _body.GetBodyChildrenOfType(body, parentType)
            .FirstOrDefault(part => part.Component.Symmetry == parentSymmetry)
            .Id;
        if (!parent.Valid)
            return false;

        if (!_body.CanInsertOrgan(parent, replacement, slot, ignoreOccupied: true))
            return false;

        var hadOldOrgan = _body.TryRemoveOrgan(parent, slot, out var oldOrgan);
        if (!_body.TryInsertOrgan(parent, replacement, slot))
        {
            if (hadOldOrgan)
                _body.TryInsertOrgan(parent, oldOrgan, slot);
            return false;
        }

        if (hadOldOrgan)
            _transform.SetCoordinates(oldOrgan, Transform(body).Coordinates);

        return true;
    }
}
