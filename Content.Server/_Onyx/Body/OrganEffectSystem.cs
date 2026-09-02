using System.Linq;
using Content.Shared._Onyx.Body;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared._Onyx.Speech;
using Content.Shared.FixedPoint;
using Content.Shared._Onyx.Medical.Surgery;
using Content.Shared._Onyx.Surgery.Organs;
using Content.Shared._Onyx.Surgery.Augments.NeuroInterface;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding;
using Content.Shared.StatusEffectNew;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Body;

[RegisterComponent]
public sealed partial class OrganEffectOwnershipComponent : Component
{
    [DataField]
    public Dictionary<string, EntityUid> Sources = new();
}

/// <summary>
/// Generic organ pipeline: applies functional organ effects and missing-organ
/// consequences whenever an organ is inserted into or removed from a body.
/// </summary>
public sealed partial class OrganEffectSystem : EntitySystem
{
    private const float MissingHeadNormalDuration = 15f;
    private const float MissingHeadStasisDuration = 300f;
    private const float MissingHeartNormalDuration = 30f;
    private const float MissingHeartStasisDuration = 300f;

    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    private static readonly EntProtoId SurgicallyMutedEffect = "StatusEffectSurgicallyMuted";
    private readonly HashSet<EntityUid> _syncingEyeDamage = new();

    private readonly HashSet<EntityUid> _pendingBodies = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrganComponent, OrganGotRemovedEvent>(OnOrganRemoved);
        SubscribeLocalEvent<OrganComponent, OrganGotInsertedEvent>(OnOrganInserted);
        SubscribeLocalEvent<OrganComponent, OrganFunctionChangedEvent>(OnOrganFunctionChanged);
        SubscribeLocalEvent<OrganComponent, NeuroBandwidthEfficiencyChangedEvent>(OnNeuroEfficiencyChanged);
        SubscribeLocalEvent<BodyComponent, EyeDamageChangedEvent>(OnEyeDamageChanged);
        SubscribeLocalEvent<MissingEyesComponent, CanSeeAttemptEvent>(OnCanSee);
    }

    private void OnOrganRemoved(Entity<OrganComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (TryComp(ent, out EyesComponent? eyes) && TryComp(args.Target, out BlindableComponent? blindable))
        {
            eyes.Damage = _blindable.GetEyeDamage((args.Target, blindable));
            eyes.MinDamage = HasComp<PermanentBlindnessComponent>(args.Target) ? 0 : blindable.MinDamage;
            Dirty(ent.Owner, eyes);
            SetBodyEyeState(args.Target, blindable, 0, 0);
        }
        if (HasComp<BodyComponent>(args.Target))
        {
            var anatomy = EnsureComp<BodyAnatomyComponent>(args.Target);
            if (!anatomy.AnatomyInitialized && CompOrNull<BodyPartComponent>(ent) is { } part)
                anatomy.RequiredParts[part.PartType] = anatomy.RequiredParts.GetValueOrDefault(part.PartType) + 1;
            if (!anatomy.AnatomyInitialized && ent.Comp.Category is { } category)
                anatomy.RequiredOrgans[category] = anatomy.RequiredOrgans.GetValueOrDefault(category) + 1;
        }

        _pendingBodies.Add(args.Target);
    }

    private void OnOrganInserted(Entity<OrganComponent> ent, ref OrganGotInsertedEvent args)
    {
        if (TryComp(ent, out EyesComponent? eyes) && TryComp(args.Target, out BlindableComponent? blindable))
            SetBodyEyeState(args.Target, blindable, eyes.Damage, eyes.MinDamage);
        _pendingBodies.Add(args.Target);
    }

    private void OnEyeDamageChanged(Entity<BodyComponent> body, ref EyeDamageChangedEvent args)
    {
        if (_syncingEyeDamage.Contains(body.Owner) || !_body.TryGetOrgan(body.Owner, "Eyes", out var eyesEntity) ||
            !TryComp(eyesEntity, out EyesComponent? eyes))
            return;

        eyes.Damage = args.Damage;
        Dirty(eyesEntity, eyes);
    }

    private void SetBodyEyeState(EntityUid body, BlindableComponent blindable, int damage, int minDamage)
    {
        if (TryComp(body, out PermanentBlindnessComponent? permanent))
            minDamage = Math.Max(minDamage, permanent.Blindness == 0 ? (int) BlurryVisionComponent.MaxMagnitude : permanent.Blindness);

        _syncingEyeDamage.Add(body);
        try
        {
            _blindable.SetMinDamage((body, blindable), minDamage);
            _blindable.AdjustEyeDamage((body, blindable), damage - _blindable.GetEyeDamage((body, blindable)));
        }
        finally
        {
            _syncingEyeDamage.Remove(body);
        }
    }

    private void OnOrganFunctionChanged(Entity<OrganComponent> ent, ref OrganFunctionChangedEvent args) =>
        _pendingBodies.Add(args.Body);

    private void OnNeuroEfficiencyChanged(Entity<OrganComponent> ent, ref NeuroBandwidthEfficiencyChangedEvent args)
    {
        if (ent.Comp.Body is { } body)
            _pendingBodies.Add(body);
    }

    private void RaiseBodyOrgansChanged(EntityUid body)
    {
        var ev = new BodyOrgansChangedEvent(body);
        RaiseLocalEvent(body, ref ev);
    }

    private void OnCanSee(Entity<MissingEyesComponent> ent, ref CanSeeAttemptEvent args)
    {
        args.Cancel();
    }

    private void RefreshBody(EntityUid body)
    {
        if (!TryComp(body, out BodyComponent? _))
            return;

        _body.InitializeAnatomy(body);
        var anatomy = Comp<BodyAnatomyComponent>(body);

        var organs = new List<(EntityUid Id, OrganComponent Component)>();
        var partCounts = new Dictionary<BodyPartType, int>();
        foreach (var (partId, part) in _body.GetBodyChildren(body))
        {
            partCounts[part.PartType] = partCounts.GetValueOrDefault(part.PartType) + 1;
            if (TryComp(partId, out OrganComponent? organ))
                organs.Add((partId, organ));
        }
        organs.AddRange(_body.GetBodyOrgans(body));
        organs.Sort(CompareOrganProviders);

        var organCounts = new Dictionary<ProtoId<OrganCategoryPrototype>, int>();
        var desired = new Dictionary<string, (EntityUid Source, EntityPrototype.ComponentRegistryEntry Entry)>();
        foreach (var (organId, organ) in organs)
        {
            if (organ.Health <= FixedPoint2.Zero)
                continue;
            if (organ.Category is { } category)
                organCounts[category] = organCounts.GetValueOrDefault(category) + 1;
            if (!TryComp(organId, out FunctionalOrganComponent? functional))
                continue;
            if (TryComp(organId, out NeuroBandwidthRuntimeComponent? runtime) && runtime.Efficiency <= 0f)
                continue;

            foreach (var (name, entry) in functional.Components)
                desired.TryAdd(name, (organId, entry));
        }

        SetMissing<MissingHeadComponent>(body, MissingPart(anatomy, partCounts, BodyPartType.Head));
        SetMissing<MissingEyesComponent>(body, MissingOrgan(anatomy, organCounts, "Eyes"));
        SetMissing<MissingEarsComponent>(body, MissingOrgan(anatomy, organCounts, "Ears"));
        SetMissing<TonguelessAccentComponent>(body, MissingOrgan(anatomy, organCounts, "Tongue"));
        var cutVocalCords = organs.Any(organ => organ.Component.Health > FixedPoint2.Zero &&
            TryComp(organ.Id, out TongueComponent? tongue) && tongue is { VocalCordsCut: true });
        if (cutVocalCords)
            _statusEffects.TrySetStatusEffectDuration(body, SurgicallyMutedEffect);
        else
            _statusEffects.TryRemoveStatusEffect(body, SurgicallyMutedEffect);
        SetMissingHeart(body, MissingOrgan(anatomy, organCounts, "Heart"));
        var ownership = EnsureComp<OrganEffectOwnershipComponent>(body);
        ReconcileOnAdd(body, desired, ownership);
        _blindable.UpdateIsBlind(body);
        RaiseBodyOrgansChanged(body);
    }

    private void SetMissing<T>(EntityUid body, bool missing) where T : Component, new()
    {
        if (missing)
            EnsureComp<T>(body);
        else
            RemComp<T>(body);
    }

    private void SetMissingHeart(EntityUid body, bool missing)
    {
        if (!missing)
        {
            RemComp<MissingHeartComponent>(body);
            return;
        }

        if (HasComp<MissingHeartComponent>(body))
            return;

        var component = EnsureComp<MissingHeartComponent>(body);
        component.NormalDuration = MissingHeartNormalDuration;
        component.StasisDuration = MissingHeartStasisDuration;
        Dirty(body, component);
    }

    private static bool MissingPart(
        BodyAnatomyComponent anatomy,
        Dictionary<BodyPartType, int> current,
        BodyPartType type)
    {
        return anatomy.RequiredParts.TryGetValue(type, out var required) &&
               current.GetValueOrDefault(type) < required;
    }

    private static bool MissingOrgan(
        BodyAnatomyComponent anatomy,
        Dictionary<ProtoId<OrganCategoryPrototype>, int> current,
        ProtoId<OrganCategoryPrototype> category)
    {
        return anatomy.RequiredOrgans.TryGetValue(category, out var required) &&
               current.GetValueOrDefault(category) < required;
    }

    private int CompareOrganProviders(
        (EntityUid Id, OrganComponent Component) left,
        (EntityUid Id, OrganComponent Component) right)
    {
        var leftPrototype = MetaData(left.Id).EntityPrototype?.ID ?? string.Empty;
        var rightPrototype = MetaData(right.Id).EntityPrototype?.ID ?? string.Empty;
        var result = string.Compare(leftPrototype, rightPrototype, StringComparison.Ordinal);
        return result != 0 ? result : left.Id.Id.CompareTo(right.Id.Id);
    }

    private void ReconcileOnAdd(EntityUid body,
        Dictionary<string, (EntityUid Source, EntityPrototype.ComponentRegistryEntry Entry)> desired,
        OrganEffectOwnershipComponent ownership)
    {
        foreach (var name in new List<string>(ownership.Sources.Keys))
        {
            if (desired.ContainsKey(name))
                continue;

            if (Factory.TryGetRegistration(name, out var registration))
                RemComp(body, registration.Type);
            ownership.Sources.Remove(name);
        }

        foreach (var (name, provided) in desired)
        {
            var type = provided.Entry.Component.GetType();
            if (ownership.Sources.TryGetValue(name, out var source))
            {
                if (source == provided.Source && HasComp(body, type))
                    continue;

                EntityManager.AddComponents(body,
                    new ComponentRegistry { [name] = provided.Entry },
                    removeExisting: true);
                ownership.Sources[name] = provided.Source;
                continue;
            }

            if (HasComp(body, type))
                continue;

            EntityManager.AddComponents(body,
                new ComponentRegistry { [name] = provided.Entry },
                removeExisting: false);
            ownership.Sources[name] = provided.Source;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.ApplyingState && _pendingBodies.Count > 0)
        {
            var pending = new List<EntityUid>(_pendingBodies);
            _pendingBodies.Clear();
            foreach (var body in pending)
                RefreshBody(body);
        }

        var headQuery = EntityQueryEnumerator<MissingHeadComponent>();
        while (headQuery.MoveNext(out var uid, out var missing))
        {
            if (_mobState.IsDead(uid))
                continue;

            missing.Elapsed += frameTime;
            var duration = BodyStasis.IsActive(EntityManager, uid)
                ? MissingHeadStasisDuration
                : MissingHeadNormalDuration;
            if (missing.Elapsed >= duration)
                _mobState.ChangeMobState(uid, MobState.Dead);
        }

        var heartQuery = EntityQueryEnumerator<MissingHeartComponent>();
        while (heartQuery.MoveNext(out var uid, out var missing))
        {
            if (_mobState.IsDead(uid))
                continue;

            var duration = BodyStasis.IsActive(EntityManager, uid) ? missing.StasisDuration : missing.NormalDuration;
            missing.Progress += frameTime / duration;
            if (missing.Progress >= 1f)
                _mobState.ChangeMobState(uid, MobState.Dead);
        }
    }
}
