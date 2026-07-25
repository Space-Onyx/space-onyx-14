using Content.Shared.Body;
using Content.Shared.EntityEffects;
using Content.Shared.Humanoid;
using Robust.Shared.Network;

namespace Content.Shared._Onyx.EntityEffects.Effects.Transform;

public sealed partial class SexChangeEntityEffectSystem : EntityEffectSystem<HumanoidProfileComponent, SexChange>
{
    [Dependency] private HumanoidProfileSystem _humanoid = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;

    protected override void Effect(Entity<HumanoidProfileComponent> entity, ref EntityEffectEvent<SexChange> args)
    {
        if (_net.IsClient)
            return;

        var updateGender = false;
        var sex = args.Effect.NewSex;
        if (sex == null)
        {
            sex = entity.Comp.Sex switch
            {
                Sex.Male => Sex.Female,
                Sex.Female => Sex.Male,
                _ => null,
            };
            updateGender = true;
        }

        if (sex is not { } newSex || !_humanoid.SetSex(entity, newSex, updateGender))
            return;

        _visualBody.SetSex(entity.Owner, newSex);
    }
}

public sealed partial class SexChange : EntityEffectBase<SexChange>
{
    [DataField]
    public Sex? NewSex;
}
