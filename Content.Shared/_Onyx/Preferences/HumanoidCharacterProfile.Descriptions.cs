// Content taken from Wega (https://github.com/wega-team/ss14-wega), licensed under GNU GPL v3.
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Shared.Preferences;

public sealed partial class HumanoidCharacterProfile
{
    [DataField]
    public string OOCFlavorText { get; set; } = string.Empty;

    [DataField]
    public string CharacterFlavorText { get; set; } = string.Empty;

    [DataField]
    public string TagsFlavorText { get; set; } = string.Empty;

    [DataField]
    public string LinksFlavorText { get; set; } = string.Empty;

    public HumanoidCharacterProfile WithOOCFlavorText(string text) => new(this) { OOCFlavorText = text };
    public HumanoidCharacterProfile WithCharacterFlavorText(string text) => new(this) { CharacterFlavorText = text };
    public HumanoidCharacterProfile WithTagsFlavorText(string text) => new(this) { TagsFlavorText = text };
    public HumanoidCharacterProfile WithLinksFlavorText(string text) => new(this) { LinksFlavorText = text };

    private void CopyDescriptionFields(HumanoidCharacterProfile other)
    {
        OOCFlavorText = other.OOCFlavorText;
        CharacterFlavorText = other.CharacterFlavorText;
        TagsFlavorText = other.TagsFlavorText;
        LinksFlavorText = other.LinksFlavorText;
    }

    private bool DescriptionFieldsEqual(HumanoidCharacterProfile other)
    {
        return OOCFlavorText == other.OOCFlavorText &&
               CharacterFlavorText == other.CharacterFlavorText &&
               TagsFlavorText == other.TagsFlavorText &&
               LinksFlavorText == other.LinksFlavorText;
    }

    private void EnsureDescriptionFieldsValid(IConfigurationManager config)
    {
        OOCFlavorText = SanitizeDescription(OOCFlavorText, config.GetCVar(CCVars.MaxOocFlavorTextLength));
        CharacterFlavorText = SanitizeDescription(CharacterFlavorText, config.GetCVar(CCVars.MaxCharacterFlavorTextLength));
        TagsFlavorText = FormatTags(SanitizeDescription(TagsFlavorText, config.GetCVar(CCVars.MaxFlavorTagsLength)));
        LinksFlavorText = SanitizeDescription(LinksFlavorText, config.GetCVar(CCVars.MaxFlavorLinksLength));
    }

    private static string SanitizeDescription(string text, int maxLength)
    {
        var sanitized = FormattedMessage.RemoveMarkupOrThrow(text);
        return sanitized.Length > maxLength ? sanitized[..maxLength] : sanitized;
    }

    private static string FormatTags(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var rawTags = input.Split([',', ' ', '\n', '\r', '\t', ';'], StringSplitOptions.RemoveEmptyEntries);
        var tags = new List<string>(rawTags.Length);
        foreach (var rawTag in rawTags)
        {
            var tag = rawTag.Trim();
            if (!tag.StartsWith('#'))
                tag = $"#{tag}";

            if (tag.Length > 1)
                tags.Add(tag);
        }

        return string.Join(", ", tags);
    }

    private void AddDescriptionFieldsHash(ref HashCode hashCode)
    {
        hashCode.Add(OOCFlavorText);
        hashCode.Add(CharacterFlavorText);
        hashCode.Add(TagsFlavorText);
        hashCode.Add(LinksFlavorText);
    }
}
