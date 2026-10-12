using System.Linq;
using Content.Shared._Onyx.Clothing.Components;
using Content.Shared._Onyx.Clothing.Modsuits.Components;
using Content.Shared.Actions.Components;
using Content.Shared.Body;
using Content.Shared.Chemistry;
using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared._Onyx.MartialArts;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Clothing.Modsuits.Systems;

public sealed partial class ModModuleSpecialSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ModModuleSystem _modules = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    private readonly DamageModifierSet _shieldModifiers = new();

    public override void Initialize()
    {
        base.Initialize();
        foreach (var damageType in _prototypes.EnumeratePrototypes<DamageTypePrototype>())
            _shieldModifiers.Coefficients[damageType.ID] = 0;
        SubscribeLocalEvent<ModModuleSpringlockComponent, ModModuleInstalledEvent>(OnSpringlockInstalled);
        SubscribeLocalEvent<ModModuleSpringlockComponent, ModModuleUninstalledEvent>(OnSpringlockUninstalled);
        SubscribeLocalEvent<ModModuleSpringlockComponent, ModModuleActivatedEvent>(OnSpringlockActivated);
        SubscribeLocalEvent<ModModuleSpringlockControllerComponent, ClothingGotEquippedEvent>(OnSpringlockEquipped);
        SubscribeLocalEvent<ModModuleSpringlockControllerComponent, BeingUnequippedAttemptEvent>(OnSpringlockUnequip);
        SubscribeLocalEvent<ModModuleSpringlockControllerComponent, ClothingGotUnequippedEvent>(OnSpringlockUnequipped);
        SubscribeLocalEvent<ModModuleSpringlockEffectComponent, ReactionEntityEvent>(OnSpringlockReaction);
        SubscribeLocalEvent<ModModuleEnergyShieldComponent, ModModuleEnergyShieldEvent>(OnShield);
        SubscribeLocalEvent<ModModuleEnergyShieldComponent, ModModuleDeactivatedEvent>(OnShieldDeactivated);
        SubscribeLocalEvent<ModModuleEnergyShieldComponent, ModModuleUninstalledEvent>(OnShieldUninstalled);
        SubscribeLocalEvent<ModModuleEnergyShieldEffectComponent, GetMeleeTargetModifiersEvent>(OnShieldMelee,
            after: [typeof(SharedMartialArtsSystem)]);
        SubscribeLocalEvent<ModModuleEnergyShieldEffectComponent, ProjectileReflectAttemptEvent>(OnShieldProjectile);
        SubscribeLocalEvent<ModModuleEnergyShieldEffectComponent, ComponentRemove>(OnShieldRemoved);
        SubscribeLocalEvent<ModModuleSpringlockComponent, ComponentShutdown>(OnSpringlockShutdown);
        SubscribeLocalEvent<ModModuleTanningComponent, ModModuleTanningEvent>(OnTan);
        SubscribeLocalEvent<ModModuleAtrocinatorComponent, ModModuleAtrocinatorEvent>(OnAtrocinator);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ModModuleSpringlockEffectComponent>();
        while (query.MoveNext(out var wearer, out var effect))
        {
            if (effect.Triggered && !effect.Locked && now >= effect.TriggerAt)
                LockSpringlock((wearer, effect));
            if (effect.Locked && !effect.MusicPlayed && now >= effect.MusicAt)
            {
                effect.MusicPlayed = true;
                if (TryComp<ModModuleSpringlockComponent>(effect.Module, out var module) &&
                    TryComp<ActorComponent>(wearer, out var actor))
                    _audio.PlayGlobal(module.Music, actor.PlayerSession);
            }
        }
    }

    private void OnSpringlockInstalled(Entity<ModModuleSpringlockComponent> module, ref ModModuleInstalledEvent args)
    {
        EnsureComp<ModModuleSpringlockControllerComponent>(args.Controller).Module = module.Owner;
        if (TryComp<SealableClothingControlComponent>(args.Controller, out var seal) && seal.WearerEntity is { } wearer)
            AddSpringlockEffect(module.Owner, wearer);
    }

    private void OnSpringlockUninstalled(Entity<ModModuleSpringlockComponent> module, ref ModModuleUninstalledEvent args)
    {
        RemComp<ModModuleSpringlockControllerComponent>(args.Controller);
        RemoveSpringlockEffect(module.Owner);
    }

    private void OnSpringlockShutdown(Entity<ModModuleSpringlockComponent> module, ref ComponentShutdown args)
    {
        if (TryComp<ModModuleComponent>(module, out var framework) && framework.InstalledController is { } controller)
            RemComp<ModModuleSpringlockControllerComponent>(controller);
        RemoveSpringlockEffect(module.Owner);
    }

    private void OnSpringlockActivated(Entity<ModModuleSpringlockComponent> module, ref ModModuleActivatedEvent args) =>
        AddSpringlockEffect(module.Owner, args.Wearer);

    private void OnSpringlockEquipped(Entity<ModModuleSpringlockControllerComponent> controller, ref ClothingGotEquippedEvent args) =>
        AddSpringlockEffect(controller.Comp.Module, args.Wearer);

    private void OnSpringlockUnequip(Entity<ModModuleSpringlockControllerComponent> controller, ref BeingUnequippedAttemptEvent args)
    {
        if (!TryComp<ModModuleComponent>(controller.Comp.Module, out var module) || !module.Permanent)
            return;
        _popup.PopupEntity(Loc.GetString("mod-module-springlock-unequip-failed"), controller, args.UnEquipTarget);
        args.Cancel();
    }

    private void OnSpringlockUnequipped(Entity<ModModuleSpringlockControllerComponent> controller, ref ClothingGotUnequippedEvent args) =>
        RemoveSpringlockEffect(controller.Comp.Module);

    private void OnSpringlockReaction(Entity<ModModuleSpringlockEffectComponent> wearer, ref ReactionEntityEvent args)
    {
        if (wearer.Comp.Triggered || !TryComp<ModModuleSpringlockComponent>(wearer.Comp.Module, out var module) ||
            args.Method != module.LockMethod || args.Reagent.ID != module.TargetReagent)
            return;
        wearer.Comp.Triggered = true;
        wearer.Comp.TriggerAt = _timing.CurTime + module.TriggerDelay;
        _audio.PlayPredicted(module.TriggerSound, wearer, wearer);
    }

    private void LockSpringlock(Entity<ModModuleSpringlockEffectComponent> wearer)
    {
        if (!TryComp<ModModuleSpringlockComponent>(wearer.Comp.Module, out var springlock) ||
            !TryComp<ModModuleComponent>(wearer.Comp.Module, out var module))
            return;
        wearer.Comp.Locked = true;
        wearer.Comp.MusicAt = _timing.CurTime + springlock.MusicDelay;
        module.Permanent = true;
        module.CanBeDisabled = false;
        Dirty(wearer.Comp.Module, module);
        _damage.TryChangeDamage(wearer.Owner, springlock.LockDamage, true);
        _popup.PopupEntity(Loc.GetString("mod-module-springlock-locked"), wearer, wearer, PopupType.LargeCaution);
        _audio.PlayPredicted(springlock.LockSound, wearer, wearer);
        _audio.PlayPredicted(springlock.SplatSound, wearer, wearer);
    }

    private void AddSpringlockEffect(EntityUid module, EntityUid wearer)
    {
        var effect = EnsureComp<ModModuleSpringlockEffectComponent>(wearer);
        effect.Module = module;
    }

    private Entity<ModModuleSpringlockEffectComponent>? FindSpringlockEffect(EntityUid module)
    {
        var query = EntityQueryEnumerator<ModModuleSpringlockEffectComponent>();
        while (query.MoveNext(out var uid, out var effect))
            if (effect.Module == module) return (uid, effect);
        return null;
    }

    private void RemoveSpringlockEffect(EntityUid module)
    {
        if (FindSpringlockEffect(module) is { } effect)
            RemComp<ModModuleSpringlockEffectComponent>(effect.Owner);
    }

    private void OnShield(Entity<ModModuleEnergyShieldComponent> module, ref ModModuleEnergyShieldEvent args)
    {
        if (!args.Handled)
            args.Handled = TryActivateShield(module.AsNullable(), args.Performer, args.Action);
    }

    private void OnShieldDeactivated(Entity<ModModuleEnergyShieldComponent> module, ref ModModuleDeactivatedEvent args) => RemoveShield(args.Wearer, module.Owner);
    private void OnShieldUninstalled(Entity<ModModuleEnergyShieldComponent> module, ref ModModuleUninstalledEvent args) => RemoveShield(null, module.Owner);

    private void OnShieldRemoved(Entity<ModModuleEnergyShieldEffectComponent> wearer, ref ComponentRemove args) =>
        QueueDel(wearer.Comp.Effect);

    private void OnShieldMelee(Entity<ModModuleEnergyShieldEffectComponent> wearer, ref GetMeleeTargetModifiersEvent args)
    {
        if (!SustainShield(wearer))
            return;
        // Apply after attack bonuses so the last sustained hit is blocked in full too.
        args.Modifiers.Add(_shieldModifiers);
    }

    private void OnShieldProjectile(Entity<ModModuleEnergyShieldEffectComponent> wearer, ref ProjectileReflectAttemptEvent args)
    {
        if (args.Cancelled || !SustainShield(wearer))
            return;
        args.Cancelled = true;
        QueueDel(args.ProjUid);
    }

    private bool SustainShield(Entity<ModModuleEnergyShieldEffectComponent> wearer)
    {
        if (wearer.Comp.Effect == null || wearer.Comp.SustainingCount <= 0)
        {
            RemoveShield(wearer.Owner, wearer.Comp.Module);
            return false;
        }
        wearer.Comp.SustainingCount--;
        if (wearer.Comp.SustainingCount <= 0)
            RemoveShield(wearer.Owner, wearer.Comp.Module);
        return true;
    }

    private void RemoveShield(EntityUid? wearer, EntityUid module)
    {
        if (wearer is { } uid && TryComp<ModModuleEnergyShieldEffectComponent>(uid, out var direct) && direct.Module == module)
        {
            RemComp<ModModuleEnergyShieldEffectComponent>(uid);
            return;
        }
        var query = EntityQueryEnumerator<ModModuleEnergyShieldEffectComponent>();
        while (query.MoveNext(out var owner, out var effect))
        {
            if (effect.Module == module)
                RemComp<ModModuleEnergyShieldEffectComponent>(owner);
        }
    }

    private void OnTan(Entity<ModModuleTanningComponent> module, ref ModModuleTanningEvent args)
    {
        if (!args.Handled)
            args.Handled = TryTan(module.AsNullable(), args.Performer, args.Action);
    }

    private void OnAtrocinator(Entity<ModModuleAtrocinatorComponent> module, ref ModModuleAtrocinatorEvent args)
    {
        if (!args.Handled)
            args.Handled = TryActivateAtrocinator(module.AsNullable(), args.Performer, args.Action);
    }

    /// <summary>
    /// Creates a fresh shield with the configured hit budget after consuming its action charge.
    /// </summary>
    public bool TryActivateShield(Entity<ModModuleEnergyShieldComponent?> module, EntityUid performer, Entity<ActionComponent> action)
    {
        if (!Resolve(module, ref module.Comp, false) || !CanActivateShield(module, performer) ||
            !_modules.TryUseAction(module.Owner, performer, action, out _))
            return false;
        RemoveShield(performer, module.Owner);
        var shield = EnsureComp<ModModuleEnergyShieldEffectComponent>(performer);
        shield.Module = module.Owner;
        shield.SustainingCount = module.Comp!.SustainingCount;
        shield.Effect = Spawn(module.Comp.Effect, Transform(performer).Coordinates);
        _transform.SetParent(shield.Effect.Value, performer);
        return true;
    }

    /// <summary>
    /// Checks shield configuration and the current suit owner without spending charge.
    /// </summary>
    public bool CanActivateShield(Entity<ModModuleEnergyShieldComponent?> module, EntityUid performer) =>
        Resolve(module, ref module.Comp, false) && module.Comp.SustainingCount > 0 &&
        _modules.CanUseModule(module.Owner, performer, out _);

    /// <summary>
    /// Darkens supported body profiles while preserving the minimum skin-color channels.
    /// </summary>
    public bool TryTan(Entity<ModModuleTanningComponent?> module, EntityUid performer, Entity<ActionComponent> action)
    {
        if (!Resolve(module, ref module.Comp, false) || !_modules.CanUseModule(module.Owner, performer, out _) ||
            !_visualBody.TryGatherMarkingsData(performer, null, out var profiles, out _, out _) || profiles.Count == 0)
            return false;
        if (profiles.Values.All(profile => !CanTan(profile.SkinColor)))
        {
            _popup.PopupEntity(Loc.GetString("mod-module-tanning-max"), performer, performer);
            return false;
        }
        if (!_modules.TryUseAction(module.Owner, performer, action, out _))
            return false;
        foreach (var (category, profile) in profiles.ToArray())
            profiles[category] = profile with { SkinColor = TanColor(profile.SkinColor) };
        _visualBody.ApplyProfiles(performer, profiles);
        _popup.PopupEntity(Loc.GetString("mod-module-tanning-used"), performer, performer);
        return true;
    }

    /// <summary>
    /// Throws and knocks down nearby physical mobs without affecting the wearer.
    /// </summary>
    public bool TryActivateAtrocinator(Entity<ModModuleAtrocinatorComponent?> module, EntityUid performer, Entity<ActionComponent> action)
    {
        if (!Resolve(module, ref module.Comp, false) || !CanActivateAtrocinator(module, performer))
            return false;
        var targets = _lookup.GetEntitiesInRange<MobStateComponent>(Transform(performer).Coordinates, module.Comp!.Radius);
        targets.RemoveWhere(target => target.Owner == performer || !HasComp<PhysicsComponent>(target));
        if (targets.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("mod-module-atrocinator-no-targets"), performer, performer);
            return false;
        }
        if (!_modules.TryUseAction(module.Owner, performer, action, out _))
            return false;
        foreach (var target in targets)
        {
            _throwing.TryThrow(target.Owner, _random.NextVector2(), module.Comp.ThrowStrength, performer);
            _stun.TryKnockdown(target.Owner, module.Comp.KnockdownTime);
        }
        _audio.PlayPvs(module.Comp.ActivationSound, performer);
        _popup.PopupEntity(Loc.GetString("mod-module-atrocinator-used"), performer, performer);
        return true;
    }

    /// <summary>
    /// Checks pulse configuration and the active suit before looking up targets.
    /// </summary>
    public bool CanActivateAtrocinator(Entity<ModModuleAtrocinatorComponent?> module, EntityUid performer) =>
        Resolve(module, ref module.Comp, false) && module.Comp.Radius >= 0 &&
        _modules.CanUseModule(module.Owner, performer, out _);

    private static bool CanTan(Color color) => color.R > 0.3f || color.G > 0.3f || color.B > 0.3f;

    private static Color TanColor(Color color) => new(
        Math.Max(0.3f, color.R * 0.85f),
        Math.Max(0.3f, color.G * 0.85f),
        Math.Max(0.3f, color.B * 0.85f),
        color.A);
}
