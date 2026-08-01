using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server._Onyx.Speech;

public abstract partial class AnimalAccentComponent : Component
{
    public abstract List<LocId> AnimalNoises { get; }

    public virtual List<LocId> AnimalAltNoises { get; } = [];

    public virtual float AltNoiseProbability => 0f;
}

[RegisterComponent]
public sealed partial class PigAccentComponent : AnimalAccentComponent
{
    public override List<LocId> AnimalNoises { get; } =
    [
        "accent-words-pig-1",
        "accent-words-pig-2",
        "accent-words-pig-3",
        "accent-words-pig-4",
    ];
}

[RegisterComponent]
public sealed partial class FrogAccentComponent : AnimalAccentComponent
{
    public override List<LocId> AnimalNoises { get; } =
    [
        "accent-words-frog-1",
        "accent-words-frog-2",
        "accent-words-frog-3",
        "accent-words-frog-4",
    ];

    public override List<LocId> AnimalAltNoises { get; } =
    [
        "accent-words-alt-frog-1",
        "accent-words-alt-frog-2",
        "accent-words-alt-frog-3",
        "accent-words-alt-frog-4",
        "accent-words-alt-frog-5",
        "accent-words-alt-frog-6",
        "accent-words-alt-frog-7",
    ];

    public override float AltNoiseProbability => 0.05f;
}

public sealed partial class AnimalAccentSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PigAccentComponent, AccentGetEvent>(OnAccent);
        SubscribeLocalEvent<FrogAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, AnimalAccentComponent component, ref AccentGetEvent args)
    {
        if (component.AnimalNoises.Count == 0)
            return;

        if (component.AltNoiseProbability > 0f &&
            component.AnimalAltNoises.Count > 0 &&
            _random.Prob(component.AltNoiseProbability))
        {
            args.Message = Loc.GetString(_random.Pick(component.AnimalAltNoises));
            return;
        }

        args.Message = Loc.GetString(_random.Pick(component.AnimalNoises));
    }
}
