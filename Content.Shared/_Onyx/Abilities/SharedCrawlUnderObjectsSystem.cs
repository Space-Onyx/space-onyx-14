using Content.Shared.Actions;
using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Events;
using Content.Shared.Maps;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._Onyx.Abilities;

public abstract partial class SharedCrawlUnderObjectsSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CrawlUnderObjectsComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<CrawlUnderObjectsComponent, ToggleCrawlingStateEvent>(OnToggle);
        SubscribeLocalEvent<CrawlUnderObjectsComponent, AttemptClimbEvent>(OnClimbAttempt);
        SubscribeLocalEvent<CrawlUnderObjectsComponent, StandAttemptEvent>(OnStandAttempt);
        SubscribeLocalEvent<CrawlUnderObjectsComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnInit(Entity<CrawlUnderObjectsComponent> ent, ref ComponentInit args)
    {
        if (ent.Comp.ToggleAction == null)
            _actions.AddAction(ent, ref ent.Comp.ToggleAction, ent.Comp.ActionProto);
    }

    private void OnToggle(Entity<CrawlUnderObjectsComponent> ent, ref ToggleCrawlingStateEvent args)
    {
        if (args.Handled)
            return;

        var changed = ent.Comp.Enabled ? Disable(ent) : Enable(ent);
        if (!changed)
            return;

        _appearance.SetData(ent, CrawlUnderObjectsVisuals.Enabled, ent.Comp.Enabled);
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
        _popup.PopupEntity(Loc.GetString(ent.Comp.Enabled
            ? "crawl-under-objects-toggle-on"
            : "crawl-under-objects-toggle-off"), ent, ent);
        args.Handled = true;
    }

    private bool Enable(Entity<CrawlUnderObjectsComponent> ent)
    {
        if (ent.Comp.Enabled || TryComp<ClimbingComponent>(ent, out var climbing) && climbing.IsClimbing)
            return false;

        ent.Comp.Enabled = true;
        if (TryComp<FixturesComponent>(ent, out var fixtures))
        {
            foreach (var (key, fixture) in fixtures.Fixtures)
            {
                var mask = fixture.CollisionMask
                    & ~(int) CollisionGroup.HighImpassable
                    & ~(int) CollisionGroup.MidImpassable
                    | (int) CollisionGroup.InteractImpassable;
                if (mask == fixture.CollisionMask)
                    continue;

                ent.Comp.ChangedFixtures.Add((key, fixture.CollisionMask));
                _physics.SetCollisionMask(ent, key, fixture, mask, fixtures);
            }
        }

        Dirty(ent);
        return true;
    }

    private bool Disable(Entity<CrawlUnderObjectsComponent> ent)
    {
        if (!ent.Comp.Enabled || IsBlocked(ent) || TryComp<ClimbingComponent>(ent, out var climbing) && climbing.IsClimbing)
            return false;

        ent.Comp.Enabled = false;
        if (TryComp<FixturesComponent>(ent, out var fixtures))
        {
            foreach (var (key, mask) in ent.Comp.ChangedFixtures)
            {
                if (fixtures.Fixtures.TryGetValue(key, out var fixture))
                    _physics.SetCollisionMask(ent, key, fixture, mask, fixtures);
            }
        }

        ent.Comp.ChangedFixtures.Clear();
        Dirty(ent);
        return true;
    }

    private bool IsBlocked(EntityUid uid)
    {
        var tile = _turf.GetTileRef(Transform(uid).Coordinates);
        return tile is { } value && _turf.IsTileBlocked(value, CollisionGroup.MobMask);
    }

    private void OnClimbAttempt(Entity<CrawlUnderObjectsComponent> ent, ref AttemptClimbEvent args)
        => args.Cancelled |= ent.Comp.Enabled;

    private void OnStandAttempt(Entity<CrawlUnderObjectsComponent> ent, ref StandAttemptEvent args)
    {
        if (ent.Comp.Enabled && IsBlocked(ent))
            args.Cancel();
    }

    private void OnRefreshSpeed(Entity<CrawlUnderObjectsComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Enabled)
            args.ModifySpeed(ent.Comp.SpeedModifier);
    }
}
