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
        if (Replacement.Count == 0 || MinSyllables < 1 || MaxSyllables < MinSyllables)
            return "<?>";

        var output = new StringBuilder();
        var word = new StringBuilder();

        void FlushWord()
        {
            if (word.Length == 0)
                return;

            var seed = StableHash(word) ^ roundId;
            var syllables = new StringBuilder();
            var totalLength = 0;
            foreach (var replacement in Replacement)
                totalLength += replacement.Length;
            var averageLength = Math.Max(1, totalLength / Replacement.Count);
            var count = Math.Clamp((word.Length + averageLength / 2) / averageLength, MinSyllables, MaxSyllables);
            var previous = -1;
            for (var i = 0; i < count; i++)
            {
                var index = StableIndex(unchecked(seed * 397 ^ i), Replacement.Count);
                if (Replacement.Count > 1 && index == previous)
                    index = (index + 1 + StableIndex(seed + i, Replacement.Count - 1)) % Replacement.Count;
                syllables.Append(Replacement[index]);
                previous = index;
            }

            var allUpper = true;
            for (var i = 0; i < word.Length; i++)
                allUpper &= !char.IsLetter(word[i]) || char.IsUpper(word[i]);
            var titleCase = char.IsUpper(word[0]);
            for (var i = 0; i < syllables.Length; i++)
            {
                var ch = syllables[i];
                if (!char.IsLetter(ch))
                {
                    output.Append(ch);
                    continue;
                }
                ch = allUpper || titleCase && i == 0
                    ? char.ToUpperInvariant(ch)
                    : char.ToLowerInvariant(ch);
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

[Virtual]
public partial class CharacterObfuscation : ObfuscationMethod
{
    [DataField]
    public string Vowels = "аеёиоуыэюя";

    [DataField]
    public string Consonants = "бвгджзйклмнпрстфхцчшщ";

    public override string Obfuscate(string message, int roundId)
    {
        if (Vowels.Length == 0 || Consonants.Length == 0)
            return "<?>";

        var output = new StringBuilder(message.Length);
        foreach (var character in message)
        {
            if (!char.IsLetter(character))
            {
                output.Append(character);
                continue;
            }

            var alphabet = IsVowel(character) ? Vowels : Consonants;
            var seed = unchecked(char.ToLowerInvariant(character) * 397 ^ roundId);
            var index = (int) ((uint) (seed * 1103515245 + 12345) % alphabet.Length);
            if (alphabet.Length > 1 && char.ToLowerInvariant(alphabet[index]) == char.ToLowerInvariant(character))
                index = (index + 1) % alphabet.Length;
            var replacement = alphabet[index];
            output.Append(char.IsUpper(character)
                ? char.ToUpperInvariant(replacement)
                : char.ToLowerInvariant(replacement));
        }

        return output.ToString();
    }

    private static bool IsVowel(char character)
    {
        return char.ToLowerInvariant(character) is
            'а' or 'е' or 'ё' or 'и' or 'о' or 'у' or 'ы' or 'э' or 'ю' or 'я' or
            'a' or 'e' or 'i' or 'o' or 'u' or 'y';
    }
}
