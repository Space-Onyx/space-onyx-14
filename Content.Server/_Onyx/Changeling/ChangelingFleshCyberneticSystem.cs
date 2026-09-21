// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Changeling;
using Content.Shared.Cloning.Events;
using Content.Shared._Onyx.Changeling;
using Content.Shared._Onyx.Cybernetics;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Changeling;

public sealed partial class ChangelingFleshCyberneticSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private MetaDataSystem _metaData = default!;

    private const string ChangelingCloningSettings = "ChangelingCloningSettings";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, CloningEvent>(OnCloning);
        SubscribeLocalEvent<BodyComponent, AfterChangelingTransformEvent>(OnAfterTransform);
    }

    private void OnCloning(Entity<BodyComponent> source, ref CloningEvent args)
    {
        if (args.Settings.ID != ChangelingCloningSettings)
            return;

        CopyCyberneticDisguises(source, args.CloneUid);
    }

    private void OnAfterTransform(Entity<BodyComponent> target, ref AfterChangelingTransformEvent args)
    {
        CopyCyberneticDisguises(args.StoredIdentity, target);
    }

    private void CopyCyberneticDisguises(EntityUid source, EntityUid target)
    {
        var targetOrgans = GetOrgansByCategory(target);
        var disguisedCategories = new HashSet<ProtoId<OrganCategoryPrototype>>();

        foreach (var sourceOrgan in EnumerateOrgans(source))
        {
            if (!TryGetDisguisePrototype(sourceOrgan, out var prototype) ||
                GetCategory(sourceOrgan) is not { } category ||
                !targetOrgans.TryGetValue(category, out var targetOrgan) ||
                HasComp<CyberneticsComponent>(targetOrgan))
                continue;

            disguisedCategories.Add(category);
            CopyDisguise(sourceOrgan, targetOrgan, prototype);
        }

        foreach (var (category, targetOrgan) in targetOrgans)
        {
            if (disguisedCategories.Contains(category) || !HasComp<ChangelingFleshCyberneticComponent>(targetOrgan))
                continue;

            RemComp<ChangelingFleshCyberneticComponent>(targetOrgan);
            if (Prototype(targetOrgan) is { } prototype)
            {
                _metaData.SetEntityName(targetOrgan, prototype.Name);
                _metaData.SetEntityDescription(targetOrgan, prototype.Description);
            }
        }
    }

    private void CopyDisguise(EntityUid source, EntityUid target, EntProtoId prototype)
    {
        var disguise = EnsureComp<ChangelingFleshCyberneticComponent>(target);
        disguise.Prototype = prototype;
        Dirty(target, disguise);

        _metaData.SetEntityName(target, Name(source));
        _metaData.SetEntityDescription(target, Description(source));
    }

    private Dictionary<ProtoId<OrganCategoryPrototype>, EntityUid> GetOrgansByCategory(EntityUid body)
    {
        var organs = new Dictionary<ProtoId<OrganCategoryPrototype>, EntityUid>();
        foreach (var organ in EnumerateOrgans(body))
        {
            if (GetCategory(organ) is { } category)
                organs.TryAdd(category, organ);
        }

        return organs;
    }

    private IEnumerable<EntityUid> EnumerateOrgans(EntityUid body)
    {
        foreach (var (part, _) in _body.GetBodyChildren(body))
            yield return part;

        foreach (var (organ, _) in _body.GetBodyOrgans(body))
            yield return organ;
    }

    private ProtoId<OrganCategoryPrototype>? GetCategory(EntityUid organ)
    {
        if (TryComp(organ, out BodyPartComponent? part))
            return part.Category;

        return TryComp(organ, out OrganComponent? internalOrgan) ? internalOrgan.Category : null;
    }

    private bool TryGetDisguisePrototype(EntityUid organ, out EntProtoId prototype)
    {
        if (TryComp(organ, out ChangelingFleshCyberneticComponent? disguise))
        {
            prototype = disguise.Prototype;
            return true;
        }

        if (HasComp<CyberneticsComponent>(organ) && Prototype(organ)?.ID is { } cyberneticPrototype)
        {
            prototype = cyberneticPrototype;
            return true;
        }

        prototype = default;
        return false;
    }
}
