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
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
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

namespace Content.Server._Onyx.Clothing.Modsuits.Systems;

public sealed partial class ModModuleSpecialSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private PowerCellSystem _power = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;
    [Dependency] private ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        base.Initialize();
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
        SubscribeLocalEvent<ModModuleEnergyShieldEffectComponent, AttackedEvent>(OnShieldAttacked);
        SubscribeLocalEvent<ModModuleEnergyShieldEffectComponent, ProjectileReflectAttemptEvent>(OnShieldProjectile);
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
        if (args.Handled || !TryUseAction(module.Owner, args.Performer, args.Action, out _))
            return;
        RemoveShield(args.Performer, module.Owner);
        var shield = EnsureComp<ModModuleEnergyShieldEffectComponent>(args.Performer);
        shield.Module = module.Owner;
        shield.SustainingCount = module.Comp.SustainingCount;
        shield.Effect = Spawn(module.Comp.Effect, Transform(args.Performer).Coordinates);
        _transform.SetParent(shield.Effect.Value, args.Performer);
        args.Handled = true;
    }

    private void OnShieldDeactivated(Entity<ModModuleEnergyShieldComponent> module, ref ModModuleDeactivatedEvent args) => RemoveShield(args.Wearer, module.Owner);
    private void OnShieldUninstalled(Entity<ModModuleEnergyShieldComponent> module, ref ModModuleUninstalledEvent args) => RemoveShield(null, module.Owner);

    private void OnShieldAttacked(Entity<ModModuleEnergyShieldEffectComponent> wearer, ref AttackedEvent args)
    {
        if (args.User == args.Used && HasComp<DamageOtherOnHitComponent>(args.Used))
            return;
        if (!SustainShield(wearer))
            return;
        args.BonusDamage = -_melee.GetDamage(args.Used, args.User);
    }

    private void OnShieldProjectile(Entity<ModModuleEnergyShieldEffectComponent> wearer, ref ProjectileReflectAttemptEvent args)
    {
        if (!SustainShield(wearer))
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
            QueueDel(direct.Effect);
            RemComp<ModModuleEnergyShieldEffectComponent>(uid);
            return;
        }
        var query = EntityQueryEnumerator<ModModuleEnergyShieldEffectComponent>();
        while (query.MoveNext(out var owner, out var effect))
            if (effect.Module == module) { QueueDel(effect.Effect); RemComp<ModModuleEnergyShieldEffectComponent>(owner); }
    }

    private void OnTan(Entity<ModModuleTanningComponent> module, ref ModModuleTanningEvent args)
    {
        if (args.Handled || !_visualBody.TryGatherMarkingsData(args.Performer, null, out var profiles, out _, out _) || profiles.Count == 0)
            return;
        const float minimum = 0.3f;
        if (profiles.Values.All(x => x.SkinColor.R <= minimum && x.SkinColor.G <= minimum && x.SkinColor.B <= minimum))
        {
            _popup.PopupEntity(Loc.GetString("mod-module-tanning-max"), args.Performer, args.Performer);
            return;
        }
        if (!TryUseAction(module.Owner, args.Performer, args.Action, out _))
            return;
        foreach (var (category, profile) in profiles.ToArray())
        {
            var color = profile.SkinColor;
            profiles[category] = profile with { SkinColor = new Color(Math.Max(minimum, color.R * 0.85f), Math.Max(minimum, color.G * 0.85f), Math.Max(minimum, color.B * 0.85f), color.A) };
        }
        _visualBody.ApplyProfiles(args.Performer, profiles);
        _popup.PopupEntity(Loc.GetString("mod-module-tanning-used"), args.Performer, args.Performer);
        args.Handled = true;
    }

    private void OnAtrocinator(Entity<ModModuleAtrocinatorComponent> module, ref ModModuleAtrocinatorEvent args)
    {
        if (args.Handled)
            return;
        var targets = _lookup.GetEntitiesInRange<MobStateComponent>(Transform(args.Performer).Coordinates, module.Comp.Radius);
        if (targets.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("mod-module-atrocinator-no-targets"), args.Performer, args.Performer);
            return;
        }
        if (!TryUseAction(module.Owner, args.Performer, args.Action, out _))
            return;
        foreach (var target in targets)
        {
            if (!HasComp<PhysicsComponent>(target))
                continue;
            _throwing.TryThrow(target.Owner, _random.NextVector2(), module.Comp.ThrowStrength, args.Performer);
            _stun.TryKnockdown(target.Owner, module.Comp.KnockdownTime);
        }
        _audio.PlayPvs(module.Comp.ActivationSound, args.Performer);
        _popup.PopupEntity(Loc.GetString("mod-module-atrocinator-used"), args.Performer, args.Performer);
        args.Handled = true;
    }

    private bool TryUseAction(EntityUid moduleUid, EntityUid performer, Entity<ActionComponent> action, out EntityUid controller)
    {
        controller = default;
        if (!TryComp<ModModuleComponent>(moduleUid, out var module) || !module.Active ||
            module.InstalledController is not { } installed ||
            !TryComp<SealableClothingControlComponent>(installed, out var seal) || seal.WearerEntity != performer)
            return false;
        foreach (var definition in module.Actions)
        {
            if (!module.ActionEntities.TryGetValue(definition.Action, out var uid) || uid != action.Owner)
                continue;
            controller = installed;
            return definition.UseCost <= 0 || _power.TryUseCharge(installed, definition.UseCost, performer, predicted: true);
        }
        return false;
    }
}
