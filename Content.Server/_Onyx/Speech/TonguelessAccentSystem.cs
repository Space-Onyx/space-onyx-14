using Content.Shared._Onyx.Speech;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server._Onyx.Speech;

public sealed partial class TonguelessAccentSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;

    private const float ReplacementChance = 0.7f;

    private static readonly Dictionary<char, char> Replacements = new()
    {
        { 'р', 'в' }, { 'Р', 'В' },
        { 'л', 'у' }, { 'Л', 'У' },
        { 'т', 'ф' }, { 'Т', 'Ф' },
        { 'д', 'з' }, { 'Д', 'З' },
        { 'к', 'х' }, { 'К', 'Х' },
        { 'г', 'х' }, { 'Г', 'Х' },
        { 'б', 'м' }, { 'Б', 'М' },
        { 'п', 'ф' }, { 'П', 'Ф' },
        { 'с', 'ш' }, { 'С', 'Ш' },
        { 'з', 'ж' }, { 'З', 'Ж' },
        { 'ц', 'с' }, { 'Ц', 'С' },
        { 'ч', 'щ' }, { 'Ч', 'Щ' },
        { 'r', 'w' }, { 'R', 'W' },
        { 'l', 'w' }, { 'L', 'W' },
        { 't', 'f' }, { 'T', 'F' },
        { 'd', 'z' }, { 'D', 'Z' },
        { 'k', 'h' }, { 'K', 'H' },
        { 'g', 'h' }, { 'G', 'H' },
        { 'b', 'm' }, { 'B', 'M' },
        { 'p', 'f' }, { 'P', 'F' },
        { 's', 'h' }, { 'S', 'H' },
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TonguelessAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    private void OnAccentGet(Entity<TonguelessAccentComponent> ent, ref AccentGetEvent args)
    {
        var chars = args.Message.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (_random.Prob(ReplacementChance) && Replacements.TryGetValue(chars[i], out var replacement))
                chars[i] = replacement;
        }

        args.Message = new string(chars);
    }
}
