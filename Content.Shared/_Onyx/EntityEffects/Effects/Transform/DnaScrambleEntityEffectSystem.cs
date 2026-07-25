using Content.Shared._Onyx.Humanoid.Identity;
using Content.Shared.EntityEffects;
using Content.Shared.Humanoid;

namespace Content.Shared._Onyx.EntityEffects.Effects.Transform;

public sealed partial class DnaScrambleEntityEffectSystem : EntityEffectSystem<HumanoidProfileComponent, DnaScramble>
{
    [Dependency] private HumanoidIdentityScrambleSystem _scramble = default!;

    protected override void Effect(Entity<HumanoidProfileComponent> entity, ref EntityEffectEvent<DnaScramble> args)
    {
        _scramble.TryScramble((entity.Owner, entity.Comp));
    }
}

public sealed partial class DnaScramble : EntityEffectBase<DnaScramble>;
