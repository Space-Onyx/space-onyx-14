using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Mobs;
using Content.Shared.Toggleable;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Body;

public sealed partial class MarkingActivitySystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;
    [Dependency] private OrganActionSystem _organAction = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MarkingActivityComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MarkingActivityComponent, ToggleActionEvent>(OnToggle);
        SubscribeLocalEvent<MarkingActivityComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MarkingActivityComponent, VisualBodyMarkingsChangedEvent>(OnMarkingsChanged);
        SubscribeLocalEvent<VisualBodyComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnStartup(Entity<MarkingActivityComponent> ent, ref ComponentStartup args) => Refresh(ent);

    private void OnToggle(Entity<MarkingActivityComponent> ent, ref ToggleActionEvent args)
    {
        if (!args.Handled && SetActive(ent, !ent.Comp.Active))
            args.Handled = true;
    }

    private void OnShutdown(Entity<MarkingActivityComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.ActionOwner is { } owner)
            _actions.RemoveAction(owner, ent.Comp.ActionEntity);
    }

    private void OnMarkingsChanged(Entity<MarkingActivityComponent> ent, ref VisualBodyMarkingsChangedEvent args)
    {
        if (ent.Comp.Active)
        {
            if (HasCurrentActiveVariant(ent.Owner))
                SetActive(ent, false);
            else
            {
                ent.Comp.Active = false;
                Dirty(ent);
            }
        }

        Refresh(ent);
        _organAction.RefreshMarkingActions(ent.Owner);
    }

    private void OnMobStateChanged(Entity<VisualBodyComponent> ent, ref MobStateChangedEvent args)
    {
        if (TryComp(ent.Owner, out MarkingActivityComponent? activity) && activity.Active)
            SetActive((ent.Owner, activity), false);
    }

    private void Refresh(Entity<MarkingActivityComponent> ent)
    {
        if (!HasVariant(ent))
        {
            SetActive(ent, false);
            RemoveAction(ent);
            return;
        }

        if (ent.Comp.ActionEntity == null)
        {
            _actions.AddAction(ent.Owner, ref ent.Comp.ActionEntity, ent.Comp.Action, ent.Owner);
            ent.Comp.ActionOwner = ent.Owner;
            Dirty(ent);
        }
    }

    private bool HasVariant(Entity<MarkingActivityComponent> ent)
    {
        foreach (var organ in _body.GetBodyOrgans(ent.Owner))
        {
            if (TryComp(organ.Id, out VisualOrganMarkingsComponent? visual) &&
                visual.Markings.Values.SelectMany(markings => markings)
                    .Any(marking => TryGetVariant(marking.MarkingId, true, out _) || TryGetVariant(marking.MarkingId, false, out _)))
                return true;
        }

        return false;
    }

    private bool SetActive(Entity<MarkingActivityComponent> ent, bool active)
    {
        if (ent.Comp.Active == active)
            return false;

        var changed = false;
        foreach (var organ in _body.GetBodyOrgans(ent.Owner))
        {
            if (!TryComp(organ.Id, out VisualOrganMarkingsComponent? visual))
                continue;

            var organChanged = false;
            var markings = visual.Markings.ToDictionary(entry => entry.Key, entry => entry.Value.ToList());
            foreach (var layer in markings.Keys.ToList())
            {
                for (var i = 0; i < markings[layer].Count; i++)
                {
                    var current = markings[layer][i];
                    if (!TryGetVariant(current.MarkingId, active, out var variant))
                        continue;

                    markings[layer][i] = new Marking(variant, current.MarkingColors) { Forced = current.Forced };
                    organChanged = true;
                }
            }

            if (!organChanged)
                continue;

            _visualBody.ApplyOrganMarkings(organ.Id, markings);
            changed = true;
        }

        if (!changed)
            return false;

        ent.Comp.Active = active;
        Dirty(ent);
        return true;
    }

    private bool HasCurrentActiveVariant(EntityUid body)
    {
        foreach (var organ in _body.GetBodyOrgans(body))
        {
            if (TryComp(organ.Id, out VisualOrganMarkingsComponent? visual) &&
                visual.Markings.Values.SelectMany(markings => markings)
                    .Any(marking => TryGetVariant(marking.MarkingId, false, out _)))
                return true;
        }

        return false;
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

    private void RemoveAction(Entity<MarkingActivityComponent> ent)
    {
        if (ent.Comp.ActionOwner is { } owner)
            _actions.RemoveAction(owner, ent.Comp.ActionEntity);
        ent.Comp.ActionEntity = null;
        ent.Comp.ActionOwner = null;
        Dirty(ent);
    }
}
