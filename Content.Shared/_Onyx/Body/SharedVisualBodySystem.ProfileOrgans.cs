using System.Linq;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.Body;

public abstract partial class SharedVisualBodySystem
{
    [Dependency] private INetManager _profileOrganNet = default!;

    private void InitializeProfileOrgans() =>
        SubscribeLocalEvent<Content.Shared._Onyx.Body.ProfileGeneratedOrganComponent, OrganGotRemovedEvent>(OnProfileOrganRemoved);

    private void ReconcileProfileOrgans(EntityUid body,
        Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> markings,
        bool replace)
    {
        if (_profileOrganNet.IsClient && !IsClientSide(body) ||
            !TryComp(body, out Content.Shared._Onyx.Body.ProfileOrgansComponent? profileOrgans))
            return;

        foreach (var (category, data) in profileOrgans.Organs)
        {
            if (!markings.TryGetValue(category, out var organMarkings))
            {
                if (!replace)
                    continue;
                organMarkings = [];
            }

            var shouldExist = data.PresenceLayers.Any(layer =>
                organMarkings.TryGetValue(layer, out var selected) && selected.Count > 0);
            var parents = _bodySystem.GetBodyChildren(body)
                .Where(part => part.Component.Category == data.Parent)
                .Select(part => part.Id)
                .ToList();
            if (parents.Count != 1)
            {
                Log.Error($"Profile organ '{category}' on {ToPrettyString(body)} requires exactly one parent category '{data.Parent}', found {parents.Count}.");
                continue;
            }

            var parent = parents[0];
            var slot = category.Id;
            var hasOrgan = _bodySystem.TryGetOrganInSlot(parent, slot, out var organ);
            if (!shouldExist)
            {
                if (hasOrgan && HasComp<Content.Shared._Onyx.Body.ProfileGeneratedOrganComponent>(organ) &&
                    _bodySystem.TryRemoveOrgan(parent, slot, out organ, reparent: false))
                    Del(organ);
                continue;
            }

            if (hasOrgan)
                continue;

            organ = Spawn(data.Prototype, Transform(body).Coordinates);
            EnsureComp<Content.Shared._Onyx.Body.ProfileGeneratedOrganComponent>(organ);
            if (!_bodySystem.TryCreateOrganSlot(parent, slot) || !_bodySystem.TryInsertOrgan(parent, organ, slot))
                Del(organ);
        }
    }

    private void OnProfileOrganRemoved(
        Entity<Content.Shared._Onyx.Body.ProfileGeneratedOrganComponent> ent,
        ref OrganGotRemovedEvent args)
    {
        RemCompDeferred<Content.Shared._Onyx.Body.ProfileGeneratedOrganComponent>(ent);
    }
}
