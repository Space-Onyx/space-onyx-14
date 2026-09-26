using Content.Shared._Onyx.Speech;
using Content.Shared.Speech;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Onyx.Speech;

public sealed partial class VulgarAccentSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ILocalizationManager _localization = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VulgarAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    private void OnAccentGet(Entity<VulgarAccentComponent> ent, ref AccentGetEvent args)
    {
        if (!_prototype.TryIndex(ent.Comp.Pack, out var words))
            return;

        var split = args.Message.Split(' ');
        for (var i = 0; i < split.Length; i++)
        {
            if (_random.Prob(ent.Comp.SwearProb))
                split[i] = _localization.GetString(_random.Pick(words.Values));
        }

        args.Message = string.Join(' ', split);
    }
}
