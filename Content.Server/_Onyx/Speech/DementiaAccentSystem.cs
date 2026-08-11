using Content.Shared._Onyx.Speech;
using Content.Shared.Speech;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Random;

namespace Content.Server._Onyx.Speech;

public sealed partial class DementiaAccentSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DementiaAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, DementiaAccentComponent component, ref AccentGetEvent args)
    {
        if (string.IsNullOrEmpty(args.Message))
            return;

        var message = _replacement.ApplyReplacements(args.Message, "dementia", uid);

        if (_random.Prob(0.15f))
        {
            message = char.ToLower(message[0]) + message[1..];
            message = $"{Loc.GetString($"accent-dementia-prefix-{_random.Next(1, 6)}")} {message}";
        }

        message = char.ToUpper(message[0]) + message[1..];

        if (_random.Prob(0.3f))
            message += Loc.GetString($"accent-dementia-suffix-{_random.Next(1, 7)}");

        args.Message = message;
    }
}
