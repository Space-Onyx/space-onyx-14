// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Onyx.Kitchen.Components;
using Content.Shared._Onyx.Kitchen;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Gibbing;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Onyx.Kitchen.EntitySystems;

public sealed partial class SharpSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ContainerSystem _containers = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SharpComponent, AfterInteractEvent>(OnAfterInteract,
            before: new[] { typeof(IngestionSystem) });
        SubscribeLocalEvent<SharpComponent, SharpDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<ButcherableComponent, GetVerbsEvent<InteractionVerb>>(OnGetInteractionVerbs);
    }

    private void OnAfterInteract(EntityUid uid, SharpComponent component, AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target || !args.CanReach)
            return;

        if (TryStartButchering(uid, target, args.User, component))
            args.Handled = true;
    }

    private bool TryStartButchering(EntityUid sharpUid,
        EntityUid target,
        EntityUid user,
        SharpComponent? sharp = null)
    {
        if (!Resolve(sharpUid, ref sharp, false) ||
            !TryComp(target, out ButcherableComponent? butcherable) ||
            TryComp(target, out MobStateComponent? mobState) && !_mobState.IsDead(target, mobState) ||
            _containers.IsEntityInContainer(target) ||
            !sharp.Butchering.Add(target))
            return false;

        var doAfter = new DoAfterArgs(EntityManager,
            user,
            sharp.ButcherDelayModifier * butcherable.ButcherDelay,
            new SharpDoAfterEvent(),
            sharpUid,
            target: target,
            used: sharpUid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = user != sharpUid,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            return true;

        sharp.Butchering.Remove(target);
        return false;
    }

    private void OnDoAfter(EntityUid uid, SharpComponent component, SharpDoAfterEvent args)
    {
        if (args.Target is not { } target)
            return;

        component.Butchering.Remove(target);

        if (args.Handled ||
            args.Cancelled ||
            !TryComp(target, out ButcherableComponent? butcherable) ||
            TryComp(target, out MobStateComponent? mobState) && !_mobState.IsDead(target, mobState))
            return;

        args.Handled = true;
        if (_containers.IsEntityInContainer(target))
            return;

        var coordinates = _transform.GetMapCoordinates(target);
        foreach (var prototype in EntitySpawnCollection.GetSpawns(butcherable.SpawnedEntities, _random))
            Spawn(prototype, coordinates.Offset(_random.NextVector2(0.25f)));

        _popup.PopupEntity(Loc.GetString("refined-butchered-success",
                ("target", Identity.Entity(target, EntityManager)),
                ("tool", Identity.Entity(uid, EntityManager))),
            target,
            args.User,
            HasComp<MobStateComponent>(target) ? PopupType.LargeCaution : PopupType.Small);

        var logImpact = HasComp<MobStateComponent>(target) ? LogImpact.High : LogImpact.Low;
        _adminLogger.Add(LogType.Gib,
            logImpact,
            $"{ToPrettyString(args.User):user} butchered {ToPrettyString(target):target} with {ToPrettyString(uid):tool}");
        _gibbing.Gib(target, user: args.User);
    }

    private void OnGetInteractionVerbs(EntityUid uid,
        ButcherableComponent component,
        GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        TryComp(args.User, out SharpComponent? userSharp);
        if (userSharp == null && args.Hands == null)
            return;

        TryComp(args.Using, out SharpComponent? usingSharp);
        TryComp(uid, out MobStateComponent? mobState);
        var sharpUid = usingSharp != null ? args.Using : userSharp != null ? args.User : null;
        var targetIsAlive = mobState != null && !_mobState.IsDead(uid, mobState);
        var disabled = sharpUid == null ||
                       _containers.IsEntityInContainer(uid) ||
                       targetIsAlive;
        string? message = null;
        if (sharpUid == null)
            message = Loc.GetString("comp-kitchen-spike-need-tool-quality", ("quality", "sharp"), ("target", uid));
        else if (targetIsAlive)
            message = Loc.GetString("refined-slice-verb-target-isnt-dead");

        args.Verbs.Add(new InteractionVerb
        {
            Act = () => TryStartButchering(sharpUid!.Value, args.Target, args.User),
            Message = message,
            Disabled = disabled,
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/cutlery.svg.192dpi.png")),
            Text = Loc.GetString("sharp-butcher-verb"),
        });
    }
}
