using System.Collections.Generic;
using System.Text;

namespace Content.Server._Onyx.Chat;

internal static class InlineActionFormatter
{
    private const char ActionDelimiter = '*';

    public static string Format(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        StringBuilder? builder = null;
        var cursor = 0;
        var searchStart = 0;

        while (searchStart < message.Length)
        {
            var openIndex = message.IndexOf(ActionDelimiter, searchStart);
            if (openIndex == -1)
                break;

            var closeIndex = message.IndexOf(ActionDelimiter, openIndex + 1);
            if (closeIndex == -1)
                break;

            if (!IsBoundaryPair(message, openIndex, closeIndex))
            {
                searchStart = openIndex + 1;
                continue;
            }

            var actionText = message[(openIndex + 1)..closeIndex].Trim();
            if (actionText.Length == 0)
            {
                searchStart = closeIndex + 1;
                continue;
            }

            builder ??= new StringBuilder(message.Length + 16);

            if (openIndex > cursor)
                builder.Append(message, cursor, openIndex - cursor);

            builder.Append("[italic]");
            builder.Append(actionText);
            builder.Append("[/italic]");

            cursor = closeIndex + 1;
            searchStart = cursor;
        }

        if (builder == null)
            return message;

        if (cursor < message.Length)
            builder.Append(message, cursor, message.Length - cursor);

        return builder.ToString();
    }


    public static string ProtectActions(
        string message,
        out List<(string placeholder, string original)>? replacements,
        out List<string>? extractedActions)
    {
        replacements = null;
        extractedActions = null;

        if (string.IsNullOrEmpty(message))
            return message;

        StringBuilder? builder = null;
        var cursor = 0;
        var searchStart = 0;
        var index = 0;

        while (searchStart < message.Length)
        {
            var openIndex = message.IndexOf(ActionDelimiter, searchStart);
            if (openIndex == -1)
                break;

            var closeIndex = message.IndexOf(ActionDelimiter, openIndex + 1);
            if (closeIndex == -1)
                break;

            if (!IsBoundaryPair(message, openIndex, closeIndex))
            {
                searchStart = openIndex + 1;
                continue;
            }

            var actionText = message[(openIndex + 1)..closeIndex].Trim();
            if (actionText.Length == 0)
            {
                searchStart = closeIndex + 1;
                continue;
            }

            replacements ??= new List<(string, string)>();
            extractedActions ??= new List<string>();
            builder ??= new StringBuilder(message.Length);

            var placeholder = $"\uFFFC{index}\uFFFC";
            var original = message[openIndex..(closeIndex + 1)];
            replacements.Add((placeholder, original));
            extractedActions.Add(actionText);

            builder.Append(message, cursor, openIndex - cursor);
            builder.Append(placeholder);

            cursor = closeIndex + 1;
            searchStart = cursor;
            index++;
        }

        if (builder == null)
            return message;

        if (cursor < message.Length)
            builder.Append(message, cursor, message.Length - cursor);

        return builder.ToString();
    }

    public static string RestoreActions(string message, List<(string placeholder, string original)>? replacements)
    {
        if (replacements == null)
            return message;

        foreach (var (placeholder, original) in replacements)
        {
            message = message.Replace(placeholder, original);
        }

        return message;
    }

    private static bool IsBoundaryPair(string message, int openIndex, int closeIndex)
    {
        if (closeIndex <= openIndex + 1)
            return false;

        var leftBoundary = openIndex == 0
                           || char.IsWhiteSpace(message[openIndex - 1])
                           || char.IsPunctuation(message[openIndex - 1]);

        var rightBoundary = closeIndex == message.Length - 1
                            || char.IsWhiteSpace(message[closeIndex + 1])
                            || char.IsPunctuation(message[closeIndex + 1]);

        return leftBoundary && rightBoundary;
    }
}