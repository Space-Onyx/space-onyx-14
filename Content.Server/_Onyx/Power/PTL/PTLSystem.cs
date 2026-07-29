using System.Numerics;
using System.Text;
using Content.Server.Flash;
using Content.Server.Popups;
using Content.Server.Power.SMES;
using Content.Server.Radiation.Systems;
using Content.Server.Stack;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._Onyx.Power.PTL;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Radiation.Components;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Power.PTL;

public sealed partial class PTLSystem : EntitySystem
{
    [Dependency] private GunSystem _gun = default!;
    [Dependency] private IGameTiming _time = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private FlashSystem _flash = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private EmagSystem _emag = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private RadiationSystem _radiation = default!;

    [ValidatePrototypeId<StackPrototype>] private readonly string _creditStack = "Credit";
    [ValidatePrototypeId<TagPrototype>] private readonly string _screwdriverTag = "Screwdriver";
    [ValidatePrototypeId<TagPrototype>] private readonly string _multitoolTag = "Multitool";

    private readonly SoundPathSpecifier _cashSound = new("/Audio/Effects/kaching.ogg");
    private readonly SoundPathSpecifier _sparkSound = new("/Audio/Effects/sparks4.ogg");
    private readonly SoundPathSpecifier _powerSound = new("/Audio/Effects/tesla_consume.ogg");

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(SmesSystem));
        SubscribeLocalEvent<PTLComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<PTLComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
        SubscribeLocalEvent<PTLComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<PTLComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<PTLComponent, SelfBeforeGunShotEvent>(OnBeforeShot);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<PTLComponent>();
        while (query.MoveNext(out var uid, out var ptl))
        {
            if (_time.CurTime > ptl.RadDecayTimer)
            {
                ptl.RadDecayTimer = _time.CurTime + TimeSpan.FromSeconds(1);
                DecayRadiation(uid);
            }

            if (!ptl.Active || _time.CurTime <= ptl.NextShotAt)
                continue;

            ptl.NextShotAt = _time.CurTime + TimeSpan.FromSeconds(ptl.ShootDelay);
            TryShoot((uid, ptl));
        }
    }

    private void DecayRadiation(EntityUid uid)
    {
        if (TryComp<RadiationSourceComponent>(uid, out var radiation) && radiation.Intensity > 0)
            _radiation.SetIntensity((uid, radiation), MathF.Max(0, radiation.Intensity - (radiation.Intensity * 0.2f + 0.1f)));
    }

    private void TryShoot(Entity<PTLComponent> ent)
    {
        if (!TryComp<BatteryComponent>(ent, out var battery) ||
            !TryComp<BatteryAmmoProviderComponent>(ent, out var provider) ||
            !TryComp<GunComponent>(ent, out var gun) ||
            _battery.GetCharge((ent.Owner, battery)) < ent.Comp.MinShootPower)
            return;

        var chargeBefore = _battery.GetCharge((ent.Owner, battery));
        provider.FireCost = (float) Math.Min(chargeBefore, ent.Comp.MaxEnergyPerShot);
        if (provider.FireCost <= 0)
            return;

        Dirty(ent.Owner, provider);
        var direction = ent.Comp.ReversedFiring ? Vector2.UnitY : -Vector2.UnitY;
        var transform = Transform(ent);
        var target = transform.Coordinates.Offset(transform.LocalRotation.RotateVec(direction));
        _gun.AttemptShoot(ent, (ent.Owner, gun), target);

        var usedMegajoules = Math.Max(0, chargeBefore - _battery.GetCharge((ent.Owner, battery))) / 1e6;
        if (usedMegajoules <= 0)
            return;

        var payout = (int) (usedMegajoules * 500 / (Math.Log(usedMegajoules * 5) + 1));
        if (!double.IsFinite(payout) || payout < 0)
            return;

        var evil = (float) (usedMegajoules * ent.Comp.EvilMultiplier);
        if (TryComp<RadiationSourceComponent>(ent, out var radiation))
            _radiation.SetIntensity((ent.Owner, radiation), evil);

        _flash.FlashArea(ent, ent, evil / 2, TimeSpan.FromSeconds(evil / 2));
        ent.Comp.SpesosHeld += payout;
        Dirty(ent);
    }

    private void OnBeforeShot(Entity<PTLComponent> ent, ref SelfBeforeGunShotEvent args)
    {
        if (!TryComp<BatteryAmmoProviderComponent>(ent, out var provider))
            return;

        var damage = ent.Comp.BaseBeamDamage * (provider.FireCost / 1e6f) * 2f;
        foreach (var (ammo, _) in args.Ammo)
        {
            if (ammo is { } uid && TryComp<HitscanBasicDamageComponent>(uid, out var hitscanDamage))
                hitscanDamage.Damage = damage;
        }
    }

    private void OnInteractHand(Entity<PTLComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        ent.Comp.Active = !ent.Comp.Active;
        var state = Loc.GetString(ent.Comp.Active ? "ptl-enabled" : "ptl-disabled");
        _popup.PopupEntity(Loc.GetString("ptl-interact-enabled", ("enabled", state)), ent, args.User);
        _audio.PlayPvs(_powerSound, args.User);
        Dirty(ent);
        args.Handled = true;
    }

    private void OnAfterInteractUsing(Entity<PTLComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (_tag.HasTag(args.Used, _screwdriverTag))
        {
            var delay = ent.Comp.ShootDelay + ent.Comp.ShootDelayIncrement;
            ent.Comp.ShootDelay = delay > ent.Comp.ShootDelayThreshold.Max
                ? ent.Comp.ShootDelayThreshold.Min
                : delay;
            _popup.PopupEntity(Loc.GetString("ptl-interact-screwdriver", ("delay", ent.Comp.ShootDelay)), ent, args.User);
            _audio.PlayPvs(_sparkSound, args.User);
            Dirty(ent);
            args.Handled = true;
            return;
        }

        if (!_tag.HasTag(args.Used, _multitoolTag) || !Transform(ent).Anchored)
            return;

        if (ent.Comp.SpesosHeld > 0)
            _stack.SpawnAtPosition((int) ent.Comp.SpesosHeld, _prototypes.Index<StackPrototype>(_creditStack), Transform(args.User).Coordinates);
        ent.Comp.SpesosHeld = 0;
        _popup.PopupEntity(Loc.GetString("ptl-interact-spesos"), ent, args.User);
        _audio.PlayPvs(_cashSound, args.User);
        Dirty(ent);
        args.Handled = true;
    }

    private void OnExamine(Entity<PTLComponent> ent, ref ExaminedEvent args)
    {
        var state = Loc.GetString(ent.Comp.Active ? "ptl-enabled" : "ptl-disabled");
        var text = new StringBuilder();
        text.AppendLine(Loc.GetString("ptl-examine-enabled", ("enabled", state)));
        text.AppendLine(Loc.GetString("ptl-examine-spesos", ("spesos", ent.Comp.SpesosHeld)));
        text.AppendLine(Loc.GetString("ptl-examine-screwdriver"));
        args.PushMarkup(text.ToString());
    }

    private void OnEmagged(Entity<PTLComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction) ||
            _emag.CheckFlag(ent, EmagType.Interaction) ||
            ent.Comp.ReversedFiring)
            return;

        ent.Comp.ReversedFiring = true;
        Dirty(ent);
        args.Handled = true;
    }
}
