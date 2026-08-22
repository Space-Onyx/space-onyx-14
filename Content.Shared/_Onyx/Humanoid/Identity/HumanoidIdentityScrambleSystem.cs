using Content.Shared.Body;
using Content.Shared.DetailExaminable;
using Content.Shared.Forensics.Systems;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Preferences;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Network;

namespace Content.Shared._Onyx.Humanoid.Identity;

public sealed partial class HumanoidIdentityScrambleSystem : EntitySystem
{
    [Dependency] private ForensicsSystem _forensics = default!;
    [Dependency] private HumanoidProfileSystem _humanoidProfile = default!;
    [Dependency] private IdentitySystem _identity = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;

    public bool TryScramble(Entity<HumanoidProfileComponent?> entity)
    {
        if (_net.IsClient || !Resolve(entity.Owner, ref entity.Comp, false))
            return false;

        var profile = HumanoidCharacterProfile.RandomWithSpecies(entity.Comp.Species);
        _visualBody.ApplyProfileTo(entity.Owner, profile);
        _humanoidProfile.ApplyProfileTo(entity, profile);
        _metaData.SetEntityName(entity.Owner, profile.Name, raiseEvents: false);
        _forensics.RandomizeDNA(entity.Owner);
        _forensics.RandomizeFingerprint(entity.Owner);
        RemComp<DetailExaminableComponent>(entity.Owner);
        _identity.QueueIdentityUpdate(entity.Owner);

        var ev = new DnaScrambledEvent(entity.Owner);
        RaiseLocalEvent(entity.Owner, ref ev, true);
        return true;
    }
}
