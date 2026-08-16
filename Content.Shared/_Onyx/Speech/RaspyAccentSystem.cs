using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Random;

namespace Content.Shared._Onyx.Speech;

public sealed partial class RaspyAccentSystem : RelayAccentSystem<RaspyAccentComponent>
{
    [Dependency] private IRobustRandom _random = default!;

    public override string Accentuate(string message, Entity<RaspyAccentComponent>? ent = null)
    {
        if (ent == null || ent.Value.Comp.Noises.Count == 0)
            return message;

        return Loc.GetString(_random.Pick(ent.Value.Comp.Noises));
    }
}
