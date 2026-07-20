using System.Text;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Language;

[Prototype]
public sealed partial class LanguagePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public bool IsVisibleLanguage;

    [DataField("obfuscation")]
    public ObfuscationMethod Obfuscation = new ReplacementObfuscation();

    public string Name => Loc.GetString($"language-{ID}-name");
    public string Description => Loc.GetString($"language-{ID}-description");
}

[ImplicitDataDefinitionForInheritors]
public abstract partial class ObfuscationMethod
{
    public abstract string Obfuscate(string message, int roundId);
}

[Virtual]
public partial class ReplacementObfuscation : ObfuscationMethod
{
    [DataField]
    public List<string> Replacement = new() { "<?>" };

    public override string Obfuscate(string message, int roundId)
    {
        return Replacement.Count == 0
            ? "<?>"
            : Replacement[StableIndex(StableHash(message) ^ roundId, Replacement.Count)];
    }

    protected static int StableIndex(int seed, int count)
    {
        return (int) ((uint) (seed * 1103515245 + 12345) % count);
    }

    protected static int StableHash(string value)
    {
        var hash = 17;
        foreach (var character in value)
            hash = unchecked(hash * 31 + char.ToLowerInvariant(character));
        return hash;
    }
}

public sealed partial class SyllableObfuscation : ReplacementObfuscation
{
    [DataField]
    public int MinSyllables = 1;

    [DataField]
    public int MaxSyllables = 4;

    public override string Obfuscate(string message, int roundId)
    {
        if (Replacement.Count == 0 || MaxSyllables < MinSyllables)
            return "<?>";

        var output = new StringBuilder();
        var word = new StringBuilder();

        void FlushWord()
        {
            if (word.Length == 0)
                return;

            var seed = StableHash(word) ^ roundId;
            var count = MinSyllables + StableIndex(seed, MaxSyllables - MinSyllables + 1);
            for (var i = 0; i < count; i++)
                output.Append(Replacement[StableIndex(seed + i, Replacement.Count)]);
            word.Clear();
        }

        foreach (var character in message)
        {
            if (char.IsLetterOrDigit(character))
                word.Append(character);
            else
            {
                FlushWord();
                output.Append(character);
            }
        }

        FlushWord();
        return output.ToString();
    }

    private static int StableHash(StringBuilder value)
    {
        var hash = 17;
        for (var i = 0; i < value.Length; i++)
            hash = unchecked(hash * 31 + char.ToLowerInvariant(value[i]));
        return hash;
    }
}
