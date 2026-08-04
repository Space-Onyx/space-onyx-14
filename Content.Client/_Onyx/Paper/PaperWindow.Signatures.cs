using System.Text.RegularExpressions;
using Content.Shared._Onyx.Paper;
using Robust.Shared.Utility;

namespace Content.Client.Paper.UI;

public sealed partial class PaperWindow
{
    private static readonly Regex SignaturePlaceholder = new(
        @"(?:<\s*sign\s*=\s*(\d+)\s*>|\[\s*sign\s*=\s*(\d+)\s*\])",
        RegexOptions.IgnoreCase);

    private static readonly Regex SignatureControl = new(
        @"(?:<|\[)\s*sign_(?:repeat_)?limit\s*=\s*\d+\s*(?:>|\])",
        RegexOptions.IgnoreCase);

    private static string RenderSignatures(string text, IReadOnlyList<SignatureDisplayInfo> signatures)
    {
        text = SignaturePlaceholder.Replace(text, match =>
        {
            var value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            if (!int.TryParse(value, out var index) || index < 1 || index > signatures.Count)
                return "[color=#000000]_____[/color]";

            var signature = signatures[index - 1];
            var name = FormattedMessage.EscapeText(signature.SignedName);
            var font = FormattedMessage.EscapeStringParameter(signature.FontId);
            return $"[font=\"{font}\" size={signature.FontSize}][color={signature.SignColor.ToHex()}]{name}[/color][/font]";
        });
        return SignatureControl.Replace(text, string.Empty);
    }
}
