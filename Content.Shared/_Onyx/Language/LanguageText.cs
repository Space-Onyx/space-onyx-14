// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;

namespace Content.Shared._Onyx.Language;

/// <summary>Projects text without exposing source text or interpreting consumer-specific markup.</summary>
public static class LanguageText
{
    /// <summary>Inline chat actions are opaque tokens produced by the chat formatter.</summary>
    public static int InlineActionLength(string text, int offset)
    {
        if (text[offset] != '\uFFFC')
            return 0;
        var end = offset + 1;
        while (end < text.Length && text[end] == '\uFFFC')
            end++;
        var markerLength = end - offset;
        var numberStart = end;
        while (end < text.Length && text[end] is >= '0' and <= '9')
            end++;
        if (end == numberStart || markerLength > text.Length - end)
            return 0;
        for (var i = 0; i < markerLength; i++)
        {
            if (text[end + i] != '\uFFFC')
                return 0;
        }
        return end - offset + markerLength;
    }

    /// <param name="protectedLength">Returns the length of an opaque token at an offset, or zero.</param>
    public static string Obfuscate(
        string text,
        LanguagePrototype language,
        int seed,
        Func<string, int, int>? protectedLength = null)
    {
        return Obfuscate(text, language.Obfuscation, seed, protectedLength);
    }

    public static string Obfuscate(
        string text,
        ObfuscationMethod obfuscation,
        int seed,
        Func<string, int, int>? protectedLength = null)
    {
        var result = new StringBuilder(text.Length);
        var start = 0;
        for (var i = 0; i < text.Length;)
        {
            var length = protectedLength?.Invoke(text, i) ?? 0;
            if (length <= 0)
            {
                i++;
                continue;
            }

            if (length > text.Length - i)
                throw new ArgumentOutOfRangeException(nameof(protectedLength));
            if (i > start)
                result.Append(obfuscation.Obfuscate(text[start..i], seed));
            result.Append(text, i, length);
            i += length;
            start = i;
        }
        if (start < text.Length)
            result.Append(obfuscation.Obfuscate(text[start..], seed));
        return result.ToString();
    }
}
