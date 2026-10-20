// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.IO;
using System.Linq;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;

namespace Content.Shared.Entry;

public sealed partial class EntryPoint
{
    private void RegisterPartialPrototypes()
    {
        using var stream = _resMan.ContentFileRead(new ResPath("/_Onyx/prototype_partials.yml"));
        using var reader = new StreamReader(stream, EncodingHelpers.UTF8);
        var paths = new HashSet<ResPath>();
        var registrations = new List<(ResPath Path, int Priority)>();

        foreach (var document in DataNodeParser.ParseYamlStream(reader))
        {
            if (document.Root is not SequenceDataNode entries)
                throw new InvalidDataException("Partial prototype manifest must contain a sequence.");

            foreach (var node in entries)
            {
                if (node is not MappingDataNode entry || entry.Count != 2 ||
                    !entry.TryGet<ValueDataNode>("path", out var pathNode) ||
                    !entry.TryGet<ValueDataNode>("priority", out var priorityNode) ||
                    !int.TryParse(priorityNode.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var priority))
                {
                    throw new InvalidDataException("Partial prototype entries require path and a non-negative integer priority.");
                }

                var path = new ResPath(pathNode.Value);
                if (!pathNode.Value.StartsWith("/Prototypes/", StringComparison.Ordinal) ||
                    pathNode.Value.Contains('\\') ||
                    pathNode.Value.Split('/').Any(segment => segment is "." or "..") ||
                    !paths.Add(path))
                {
                    throw new InvalidDataException($"Invalid or duplicate partial prototype directory: {pathNode.Value}");
                }

                registrations.Add((path, priority));
            }
        }

        foreach (var (path, priority) in registrations)
            _prototypeManager.PartialDirectory(path, priority);
    }
}
