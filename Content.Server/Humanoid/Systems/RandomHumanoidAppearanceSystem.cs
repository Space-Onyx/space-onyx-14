using System.Linq; // <Onyx-RandomAppearanceMarkings>
using Content.Server.Humanoid.Components;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;

namespace Content.Server.Humanoid.Systems;

public sealed partial class RandomHumanoidAppearanceSystem : EntitySystem
{
    [Dependency] private HumanoidProfileSystem _humanoidProfile = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RandomHumanoidAppearanceComponent, MapInitEvent>(OnMapInit, after: [typeof(InitialBodySystem), typeof(SharedVisualBodySystem)]); // <Onyx-RandomAppearanceColors-edited>
    }

    private void OnMapInit(EntityUid uid, RandomHumanoidAppearanceComponent component, MapInitEvent args)
    {
        // If we have an initial profile/base layer set, do not randomize this humanoid.
        if (!TryComp<HumanoidProfileComponent>(uid, out var humanoid))
            return;

        // <Onyx-RandomAppearanceMarkings-edited>
        var randomize = HumanoidCharacterProfile.RandomizeConfigAll ^ HumanoidCharacterProfile.RandomizeCfg.Species;
        var profile = HumanoidCharacterProfile.Random(randomize,
            new HumanoidCharacterProfile().WithSpecies(humanoid.Species));
        foreach (var markings in profile.Appearance.Markings.Values)
        {
            foreach (var layer in markings.Keys
                         .Where(layer => layer is not HumanoidVisualLayers.Hair and not HumanoidVisualLayers.FacialHair)
                         .ToArray())
            {
                markings.Remove(layer);
            }
        }
        // </Onyx-RandomAppearanceMarkings-edited>

        _visualBody.ApplyProfileTo(uid, profile);
        _humanoidProfile.ApplyProfileTo(uid, profile);

        if (component.RandomizeName)
            _metaData.SetEntityName(uid, profile.Name);
    }
}
