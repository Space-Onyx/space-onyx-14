using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Mobs;
using Content.Shared.Toggleable;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Body;

public sealed partial class VisualOrganActivitySystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VisualOrganActivityComponent, OrganGotInsertedEvent>(OnInserted);
        SubscribeLocalEvent<VisualOrganActivityComponent, OrganGotRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<VisualOrganActivityComponent, ToggleActionEvent>(OnToggle);
        SubscribeLocalEvent<VisualOrganActivityComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VisualBodyComponent, VisualBodyMarkingsChangedEvent>(OnMarkingsChanged);
        SubscribeLocalEvent<VisualBodyComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnInserted(Entity<VisualOrganActivityComponent> ent, ref OrganGotInsertedEvent args) => Refresh(ent, args.Target);

    private void OnRemoved(Entity<VisualOrganActivityComponent> ent, ref OrganGotRemovedEvent args)
    {
        SetActive(ent, false);
        RemoveAction(ent);
    }

    private void OnToggle(Entity<VisualOrganActivityComponent> ent, ref ToggleActionEvent args)
    {
        if (!args.Handled && SetActive(ent, !ent.Comp.Active))
            args.Handled = true;
    }

    private void OnShutdown(Entity<VisualOrganActivityComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.ActionOwner is { } owner)
            _actions.RemoveAction(owner, ent.Comp.ActionEntity);
    }

    private void OnMarkingsChanged(Entity<VisualBodyComponent> ent, ref VisualBodyMarkingsChangedEvent args)
    {
        foreach (var organ in _body.GetBodyOrgans(ent.Owner))
            if (TryComp(organ.Id, out VisualOrganActivityComponent? activity))
            {
                if (activity.Active)
                {
                    if (HasCurrentActiveVariant(organ.Id))
                        SetActive((organ.Id, activity), false);
                    else
                    {
                        activity.Active = false;
                        Dirty(organ.Id, activity);
                    }
                }
                Refresh((organ.Id, activity), ent.Owner);
            }
    }

    private void OnMobStateChanged(Entity<VisualBodyComponent> ent, ref MobStateChangedEvent args)
    {
        foreach (var organ in _body.GetBodyOrgans(ent.Owner))
            if (TryComp(organ.Id, out VisualOrganActivityComponent? activity) && activity.Active)
                SetActive((organ.Id, activity), false);
    }

    private void Refresh(Entity<VisualOrganActivityComponent> ent, EntityUid body)
    {
        if (!HasVariant(ent))
        {
            SetActive(ent, false);
            RemoveAction(ent);
            return;
        }

        if (ent.Comp.ActionEntity == null)
        {
            _actions.AddAction(body, ref ent.Comp.ActionEntity, ent.Comp.Action, ent.Owner);
            ent.Comp.ActionOwner = body;
            Dirty(ent);
        }
    }

    private bool HasVariant(Entity<VisualOrganActivityComponent> ent)
    {
        if (!TryComp(ent.Owner, out VisualOrganMarkingsComponent? visual))
            return false;

        return visual.Markings.Values.SelectMany(markings => markings)
            .Any(marking => TryGetVariant(marking.MarkingId, true, out _) || TryGetVariant(marking.MarkingId, false, out _));
    }

    private bool SetActive(Entity<VisualOrganActivityComponent> ent, bool active)
    {
        if (ent.Comp.Active == active ||
            !TryComp(ent.Owner, out VisualOrganMarkingsComponent? visual))
            return false;

        var changed = false;
        var markings = visual.Markings.ToDictionary(entry => entry.Key, entry => entry.Value.ToList());
        foreach (var layer in markings.Keys.ToList())
        {
            for (var i = 0; i < markings[layer].Count; i++)
            {
                var current = markings[layer][i];
                if (!TryGetVariant(current.MarkingId, active, out var variant))
                    continue;

                markings[layer][i] = new Marking(variant, current.MarkingColors) { Forced = current.Forced };
                changed = true;
            }
        }

        if (!changed)
            return false;

        ent.Comp.Active = active;
        Dirty(ent);
        _visualBody.ApplyOrganMarkings(ent.Owner, markings);
        return true;
    }

    private bool HasCurrentActiveVariant(EntityUid organ)
    {
        if (!TryComp(organ, out VisualOrganMarkingsComponent? visual))
            return false;

        return visual.Markings.Values.SelectMany(markings => markings)
            .Any(marking => TryGetVariant(marking.MarkingId, false, out _));
    }

    private bool TryGetVariant(ProtoId<MarkingPrototype> current, bool active, out ProtoId<MarkingPrototype> variant)
    {
        if (active && ProtoMan.TryIndex(current, out var prototype) && prototype.ActiveVariant is { } activeVariant &&
            IsValidPair(prototype, activeVariant))
        {
            variant = activeVariant;
            return true;
        }

        if (!active)
        {
            foreach (var prototypeCandidate in ProtoMan.EnumeratePrototypes<MarkingPrototype>())
            {
                if (prototypeCandidate.ActiveVariant != current)
                    continue;
                if (!IsValidPair(prototypeCandidate, current))
                    continue;
                variant = prototypeCandidate.ID;
                return true;
            }
        }

        variant = default;
        return false;
    }

    private bool IsValidPair(MarkingPrototype source, ProtoId<MarkingPrototype> target) =>
        ProtoMan.TryIndex(target, out var targetPrototype) &&
        source.BodyPart == targetPrototype.BodyPart &&
        source.Sprites.Count == targetPrototype.Sprites.Count;

    private void RemoveAction(Entity<VisualOrganActivityComponent> ent)
    {
        if (ent.Comp.ActionOwner is { } owner)
            _actions.RemoveAction(owner, ent.Comp.ActionEntity);
        ent.Comp.ActionEntity = null;
        ent.Comp.ActionOwner = null;
        Dirty(ent);
    }
}
