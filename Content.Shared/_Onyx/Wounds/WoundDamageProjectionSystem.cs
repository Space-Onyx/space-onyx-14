using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Rejuvenate;
using Robust.Shared.Network;

namespace Content.Shared._Onyx.Wounds;

public sealed partial class WoundDamageProjectionSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private PainSystem _pain = default!;

    private readonly HashSet<EntityUid> _projecting = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WoundHostComponent, MapInitEvent>(OnMapInit, after: [typeof(InitialBodySystem)]);
        SubscribeLocalEvent<WoundHostComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<WoundableComponent, DamageDealtEvent>(OnPartDamageDealt, after: [typeof(DamageableSystem)]);
    }

    private void OnMapInit(Entity<WoundHostComponent> body, ref MapInitEvent args)
    {
        SetupBody(body);
    }

    private void OnRejuvenate(Entity<WoundHostComponent> body, ref RejuvenateEvent args)
    {
        if (!_net.IsServer || !_projecting.Add(body))
            return;

        try
        {
            if (TryComp(body, out SystemicDamageComponent? systemic))
            {
                systemic.Damage = new DamageSpecifier();
                Dirty(body, systemic);
            }

            foreach (var (part, _) in _body.GetBodyChildren(body))
                if (TryComp(part, out DamageableComponent? damageable))
                {
                    _damage.ClearAllDamage((part, damageable));
                    if (TryComp(part, out PainComponent? pain))
                    {
                        _pain.SetPain((part, pain), FixedPoint2.Zero);
                        _pain.ClearPainSuppression((part, pain));
                    }

                    if (TryComp(part, out WoundableComponent? woundable) &&
                        woundable.AmputationOverflow != FixedPoint2.Zero)
                    {
                        woundable.AmputationOverflow = FixedPoint2.Zero;
                        Dirty(part, woundable);
                    }
                }

            if (TryComp(body, out PainComponent? bodyPain))
                _pain.ClearPainSuppression((body, bodyPain));
        }
        finally
        {
            _projecting.Remove(body);
        }

        RefreshBodyDamage(body);
    }

    private void OnPartDamageDealt(Entity<WoundableComponent> part, ref DamageDealtEvent args)
    {
        if (!_net.IsServer || !TryComp(part, out BodyPartComponent? component))
            return;

        _pain.ApplyDamage(part, args.Damage, component);

        if (component.Body is { } body)
            RefreshBodyDamage(body);
        else
            RefreshDetachedDamage(GetDetachedRoot(part));
    }

    public void OnPartInserted(EntityUid part, EntityUid body)
    {
        if (!HasComp<WoundHostComponent>(body))
            return;

        SetupPart(part);
        RefreshBodyPain(body);
        RefreshBodyDamage(body);
    }

    public void OnPartRemoved(EntityUid part, EntityUid body)
    {
        RefreshDetachedDamage(part);
        RefreshBodyPain(body);
        RefreshBodyDamage(body);
    }

    private EntityUid GetDetachedRoot(EntityUid part)
    {
        var root = part;
        while (CompOrNull<BodyPartComponent>(root)?.Parent is { } parent)
            root = parent;

        return root;
    }

    private void RefreshDetachedDamage(EntityUid root)
    {
        if (!_net.IsServer || !HasComp<BodyPartComponent>(root))
            return;

        var visual = EnsureComp<PartDamageVisualsComponent>(root);
        visual.Damage.Clear();
        foreach (var (part, _) in _body.GetBodyPartChildren(root))
        {
            if (!TryComp(part, out DamageableComponent? damageable) || !TryGetVisualLayer(part, out var layer))
                continue;

            var damage = _damage.GetPositiveDamage((part, damageable));
            if (!visual.Damage.TryGetValue(layer, out var current))
                visual.Damage[layer] = damage.Clone();
            else
                visual.Damage[layer] = current + damage;
        }

        Dirty(root, visual);
    }

    public void RefreshBodyDamage(EntityUid body)
    {
        if (!_net.IsServer || !HasComp<WoundHostComponent>(body) || !_projecting.Add(body))
            return;

        try
        {
            var total = new DamageSpecifier();
            if (TryComp(body, out SystemicDamageComponent? systemic))
            {
                foreach (var (type, amount) in systemic.Damage.DamageDict.ToArray())
                {
                    if (_damage.CanBeDamagedBy(body, type))
                    {
                        total.DamageDict[type] = amount;
                        continue;
                    }

                    systemic.Damage.DamageDict.Remove(type);
                    Dirty(body, systemic);
                }
            }
            var visual = EnsureComp<PartDamageVisualsComponent>(body);
            visual.Damage.Clear();
            foreach (var (part, _) in _body.GetBodyChildren(body))
            {
                if (TryComp(part, out DamageableComponent? damageable))
                {
                    var damage = _damage.GetPositiveDamage((part, damageable));
                    total += damage;
                    if (TryGetVisualLayer(part, out var layer))
                    {
                        if (!visual.Damage.TryGetValue(layer, out var current))
                            visual.Damage[layer] = damage.Clone();
                        else
                            visual.Damage[layer] = current + damage;
                    }
                }
            }

            _damage.SetDamage(body, total);
            Dirty(body, visual);
        }
        finally
        {
            _projecting.Remove(body);
        }
    }

    private void SetupBody(EntityUid body)
    {
        if (!_net.IsServer)
            return;

        EnsureComp<SystemicDamageComponent>(body);
        EnsureComp<PartDamageVisualsComponent>(body);
        EnsureComp<PainComponent>(body);
        foreach (var (part, _) in _body.GetBodyChildren(body))
            SetupPart(part);
        RefreshBodyPain(body);
        RefreshBodyDamage(body);
    }

    private void SetupPart(EntityUid part)
    {
        EnsureComp<WoundableComponent>(part);
        EnsureComp<DamageableComponent>(part);
        EnsureComp<PainComponent>(part);
        var injurable = EnsureComp<InjurableComponent>(part);
        if (injurable.DamageContainer != null)
            return;

        injurable.DamageContainer = "Biological";
        Dirty(part, injurable);
    }

    private void RefreshBodyPain(EntityUid body)
    {
        var value = FixedPoint2.Zero;
        foreach (var (part, _) in _body.GetBodyChildren(body))
            value += _pain.GetRawPain(part);
        _pain.SetPain((body, EnsureComp<PainComponent>(body)), value);
    }

    private bool TryGetVisualLayer(EntityUid part, out HumanoidVisualLayers layer)
    {
        layer = default;
        if (!TryComp(part, out BodyPartComponent? component))
            return false;

        switch (component.PartType, component.Symmetry)
        {
            case (BodyPartType.Torso or BodyPartType.Chest, _): layer = HumanoidVisualLayers.Chest; return true;
            case (BodyPartType.Groin, _): layer = HumanoidVisualLayers.Groin; return true;
            case (BodyPartType.Head, _): layer = HumanoidVisualLayers.Head; return true;
            case (BodyPartType.Arm, BodyPartSymmetry.Left): layer = HumanoidVisualLayers.LArm; return true;
            case (BodyPartType.Arm, BodyPartSymmetry.Right): layer = HumanoidVisualLayers.RArm; return true;
            case (BodyPartType.Hand, BodyPartSymmetry.Left): layer = HumanoidVisualLayers.LHand; return true;
            case (BodyPartType.Hand, BodyPartSymmetry.Right): layer = HumanoidVisualLayers.RHand; return true;
            case (BodyPartType.Leg, BodyPartSymmetry.Left): layer = HumanoidVisualLayers.LLeg; return true;
            case (BodyPartType.Leg, BodyPartSymmetry.Right): layer = HumanoidVisualLayers.RLeg; return true;
            case (BodyPartType.Foot, BodyPartSymmetry.Left): layer = HumanoidVisualLayers.LFoot; return true;
            case (BodyPartType.Foot, BodyPartSymmetry.Right): layer = HumanoidVisualLayers.RFoot; return true;
            default: return false;
        }
    }
}
