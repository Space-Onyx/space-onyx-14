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

    [DataField]
    public bool AlwaysUnderstood;

    [DataField]
    public bool RequiresSight;

    [DataField]
    public LanguageSpeechOverride Speech = new();

    [DataField("obfuscation")]
    public ObfuscationMethod Obfuscation = new ReplacementObfuscation();

    public string Name => Loc.GetString($"language-{ID}-name");
    public string Description => Loc.GetString($"language-{ID}-description");
}

[DataDefinition]
public sealed partial class LanguageSpeechOverride
{
    [DataField]
    public Color? Color;

    [DataField]
    public string? FontId;

    [DataField]
    public string? BoldFontId;

    [DataField]
    public int? FontSize;
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

            var casePattern = new bool[word.Length];
            for (var i = 0; i < word.Length; i++)
                casePattern[i] = char.IsUpper(word[i]);

            var seed = StableHash(word) ^ roundId;
            var count = MinSyllables + StableIndex(seed, MaxSyllables - MinSyllables + 1);

            var syllables = new StringBuilder();
            for (var i = 0; i < count; i++)
                syllables.Append(Replacement[StableIndex(seed + i, Replacement.Count)]);

            var lastIdx = casePattern.Length - 1;
            for (var i = 0; i < syllables.Length; i++)
            {
                var ch = syllables[i];
                if (!char.IsLetter(ch))
                {
                    output.Append(ch);
                    continue;
                }
                var caseIdx = Math.Min(i, lastIdx);
                ch = casePattern[caseIdx] ? char.ToUpperInvariant(ch) : char.ToLowerInvariant(ch);
                output.Append(ch);
            }

            word.Clear();
        }

        foreach (var character in message)
        {
            if (char.IsDigit(character))
            {
                FlushWord();
                output.Append(character);
            }
            else if (char.IsLetter(character))
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
