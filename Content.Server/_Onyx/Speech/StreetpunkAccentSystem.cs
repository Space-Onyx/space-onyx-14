using System.Text.RegularExpressions;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.EntitySystems;

namespace Content.Server._Onyx.Speech;

[RegisterComponent]
public sealed partial class StreetpunkAccentComponent : Component;

public sealed partial class StreetpunkAccentSystem : EntitySystem
{
    [Dependency] private ReplacementAccentSystem _replacement = default!;

    private static readonly Regex RegexIng = new(@"ing\b");
    private static readonly Regex RegexAnd = new(@"\band\b");
    private static readonly Regex RegexDve = new("d've");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StreetpunkAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, StreetpunkAccentComponent component, ref AccentGetEvent args)
    {
        var message = RegexIng.Replace(args.Message, "in'");
        message = RegexAnd.Replace(message, "an'");
        message = RegexDve.Replace(message, "da");
        args.Message = _replacement.ApplyReplacements(message, "streetpunk", uid);
    }
}
