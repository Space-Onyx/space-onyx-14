// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.ContentPack;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Content.Client._Onyx.Guidebook;

public sealed partial class GuidebookPreferences
{
    [Dependency] private ISerializationManager _serialization = default!;
    [Dependency] private ILogManager _log = default!;

    private static readonly ResPath PreferencesPath = new("/onyx-guidebook.yml");

    private readonly IResourceManager _resources;
    private readonly ISawmill _sawmill;

    public GuidebookWindowState Window { get; private set; } = new();
    public HashSet<string> Favorites { get; private set; } = new();

    public event Action? FavoritesChanged;

    public GuidebookPreferences(IResourceManager resources)
    {
        IoCManager.InjectDependencies(this);
        _resources = resources;
        _sawmill = _log.GetSawmill("guidebook");

        try
        {
            if (!resources.UserData.Exists(PreferencesPath))
                return;

            using var reader = resources.UserData.OpenText(PreferencesPath);
            var stream = new YamlStream();
            stream.Load(reader);
            if (stream.Documents.Count == 0)
                return;

            var saved = _serialization.Read<GuidebookSavedPreferences>(
                stream.Documents[0].RootNode.ToDataNode(), notNullableOverride: true);
            Window = saved.Window ?? new();
            Favorites = saved.Favorites ?? new HashSet<string>();
        }
        catch (Exception exception)
        {
            _sawmill.Warning($"Unable to load guidebook preferences: {exception.Message}");
        }
    }

    public void ToggleFavorite(string entry)
    {
        if (!Favorites.Remove(entry))
            Favorites.Add(entry);

        Save();
        FavoritesChanged?.Invoke();
    }

    public void SaveWindow(GuidebookWindowState state)
    {
        Window = state;
        Save();
    }

    private void Save()
    {
        try
        {
            var data = _serialization.WriteValue(
                new GuidebookSavedPreferences { Window = Window, Favorites = Favorites },
                notNullableOverride: true);
            var stream = new YamlStream(new YamlDocument(data.ToYamlNode()));
            using var writer = _resources.UserData.OpenWriteText(PreferencesPath);
            stream.Save(writer);
        }
        catch (Exception exception)
        {
            _sawmill.Warning($"Unable to save guidebook preferences: {exception.Message}");
        }
    }
}

[DataDefinition]
public sealed partial class GuidebookSavedPreferences
{
    [DataField] public GuidebookWindowState? Window { get; set; }
    [DataField] public HashSet<string>? Favorites { get; set; }
}

[DataDefinition]
public sealed partial class GuidebookWindowState
{
    [DataField] public float Width { get; set; } = 1000;
    [DataField] public float Height { get; set; } = 700;
    [DataField] public float SidebarWidth { get; set; } = 260;
    [DataField] public bool SidebarHidden { get; set; }
    [DataField] public string? Category { get; set; }
    [DataField] public string? Entry { get; set; }
    [DataField] public Dictionary<string, bool> Expanded { get; set; } = new();
    [DataField] public Dictionary<string, float> Scroll { get; set; } = new();
}
