using Content.Shared.Speech;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Utility;

namespace Content.Server._Onyx.Speech;

[RegisterComponent]
[Access(typeof(CheeseAccentSystem))]
public sealed partial class CheeseAccentComponent : Component;

public sealed partial class CheeseAccentSystem : EntitySystem
{
    [Dependency] private ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CheeseAccentComponent, AccentGetEvent>(OnAccent);
        DebugTools.Assert(Capitalize("") == "");
    }

    private void OnAccent(Entity<CheeseAccentComponent> ent, ref AccentGetEvent args)
    {
        var message = _replacement.ApplyReplacements(args.Message, "cheese", ent);
        args.Message = Capitalize(message);
    }

    internal static string Capitalize(string message)
        => message.Length == 0 ? message : char.ToUpperInvariant(message[0]) + message[1..];
}
