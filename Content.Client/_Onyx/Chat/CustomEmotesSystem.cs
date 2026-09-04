using System.Linq;
using System.Text;
using Content.Shared._Onyx.Chat;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Client._Onyx.Chat;

public sealed partial class CustomEmotesSystem : EntitySystem
{
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IResourceManager _res = default!;

    private static readonly ResPath SavePath = new("/custom_emotes.cfg");
    private static readonly ResPath TemporarySavePath = new("/custom_emotes.cfg.tmp");
    private static readonly ResPath BackupSavePath = new("/custom_emotes.cfg.bak");

    private const int SaveSchemaVersion = 1;
    private const int MaxSaveLength = 65536;
    private const int MaxEntries = 128;
    private const int MaxNameLength = 64;
    private const int MaxTextLength = 512;
    private const int MaxIdLength = 128;
    private const int MaxBindKeys = 4;

    public event Action? EntriesChanged;

    public static bool SuppressHotkeys;

    public List<CustomEmoteEntry> Entries { get; } = new();

    private readonly HashSet<int> _downKeys = new();
    private readonly HashSet<CustomEmoteEntry> _fired = new();

    public override void Initialize()
    {
        base.Initialize();
        Load();
        _input.FirstChanceOnKeyEvent += OnKeyEvent;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _input.FirstChanceOnKeyEvent -= OnKeyEvent;
    }

    public void Play(CustomEmoteEntry entry)
    {
        if (entry.Custom)
        {
            if (string.IsNullOrWhiteSpace(entry.Text))
                return;

            RaiseNetworkEvent(new PlayCustomEmoteMessage(
                entry.Text,
                entry.Range,
                entry.Radius,
                entry.Perspective,
                entry.ShowToGhosts,
                entry.SoundId));
        }
        else if (entry.EmoteId != null)
        {
            RaiseNetworkEvent(new PlayPanelEmoteMessage(entry.EmoteId));
        }
    }

    public void Add(CustomEmoteEntry entry)
    {
        if (Entries.Count >= MaxEntries || !Normalize(entry))
            return;

        Entries.Add(entry);
        Save();
        EntriesChanged?.Invoke();
    }

    public void Remove(CustomEmoteEntry entry)
    {
        Entries.Remove(entry);
        Save();
        EntriesChanged?.Invoke();
    }

    public void SaveChanges()
    {
        Save();
        EntriesChanged?.Invoke();
    }

    private void OnKeyEvent(KeyEventArgs args, KeyEventType type)
    {
        var key = (int) args.Key;

        if (type == KeyEventType.Up)
        {
            _downKeys.Remove(key);
            _fired.RemoveWhere(e => e.BindKeys.Contains(key));
            return;
        }

        if (type != KeyEventType.Down || args.IsRepeat)
            return;

        _downKeys.Add(key);

        if (SuppressHotkeys)
            return;

        if (_ui.KeyboardFocused is LineEdit or TextEdit)
            return;

        foreach (var entry in Entries)
        {
            if (entry.BindKeys.Count == 0 || !entry.BindKeys.Contains(key) || _fired.Contains(entry))
                continue;

            var match = true;
            foreach (var bind in entry.BindKeys)
            {
                if (!_downKeys.Contains(bind))
                {
                    match = false;
                    break;
                }
            }

            if (!match)
                continue;

            Play(entry);
            _fired.Add(entry);
            args.Handle();
            return;
        }
    }

    private void Load()
    {
        try
        {
            if (TryLoadConfig(SavePath) || TryLoadConfig(BackupSavePath))
                return;
        }
        catch
        {
        }
    }

    private bool TryLoadConfig(ResPath path)
    {
        if (!_res.UserData.TryReadAllText(path, out var text) || text.Length > MaxSaveLength)
            return false;

        var lines = text.Split('\n');
        if (lines.Length == 0 || lines[0].TrimEnd('\r') != $"format={SaveSchemaVersion}")
            return false;

        Entries.Clear();
        CustomEmoteEntry? current = null;
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line == "[emote]")
            {
                AddLoadedEntry(current);
                current = new CustomEmoteEntry();
                continue;
            }

            ReadEntryLine(current, line);
        }

        AddLoadedEntry(current);
        return true;
    }

    private static void ReadEntryLine(CustomEmoteEntry? entry, string line)
    {
        if (entry == null)
            return;

        var separator = line.IndexOf('=');
        if (separator <= 0)
            return;

        ReadEntryValue(entry, line[..separator], Unescape(line[(separator + 1)..]));
    }

    private static void ReadEntryValue(CustomEmoteEntry entry, string key, string value)
    {
        switch (key)
        {
            case "name": entry.Name = value; break;
            case "custom" when bool.TryParse(value, out var custom): entry.Custom = custom; break;
            case "perspective" when Enum.TryParse(value, out EmotePerspective perspective): entry.Perspective = perspective; break;
            case "range" when Enum.TryParse(value, out EmoteVisibilityRange range): entry.Range = range; break;
            case "radius" when int.TryParse(value, out var radius): entry.Radius = radius; break;
            case "showToGhosts" when bool.TryParse(value, out var ghosts): entry.ShowToGhosts = ghosts; break;
            case "bindKeys":
                entry.BindKeys.Clear();
                foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (entry.BindKeys.Count >= MaxBindKeys)
                        break;
                    if (int.TryParse(part, out var bind) && !entry.BindKeys.Contains(bind))
                        entry.BindKeys.Add(bind);
                }
                break;
            case "emoteId": entry.EmoteId = NullIfEmpty(value); break;
            case "text": entry.Text = NullIfEmpty(value); break;
            case "soundId": entry.SoundId = NullIfEmpty(value); break;
        }
    }

    private void AddLoadedEntry(CustomEmoteEntry? entry)
    {
        if (entry != null && Entries.Count < MaxEntries && Normalize(entry))
            Entries.Add(entry);
    }

    private static bool Normalize(CustomEmoteEntry entry)
    {
        entry.Name = entry.Name.Trim();
        entry.Text = NullIfEmpty(entry.Text?.Trim());
        entry.EmoteId = NullIfEmpty(entry.EmoteId?.Trim());
        entry.SoundId = NullIfEmpty(entry.SoundId?.Trim());

        if (entry.Name.Length is 0 or > MaxNameLength ||
            entry.Text?.Length > MaxTextLength ||
            entry.EmoteId?.Length > MaxIdLength ||
            entry.SoundId?.Length > MaxIdLength ||
            !Enum.IsDefined(entry.Perspective) ||
            !Enum.IsDefined(entry.Range) ||
            entry.Custom && entry.Text == null ||
            !entry.Custom && entry.EmoteId == null)
            return false;

        entry.Radius = Math.Clamp(entry.Radius, EmoteVisibilityOptions.MinRadius, EmoteVisibilityOptions.MaxRadius);
        entry.BindKeys = entry.BindKeys
            .Where(key => key != (int) Keyboard.Key.MouseLeft && key != (int) Keyboard.Key.MouseRight)
            .Distinct()
            .Take(MaxBindKeys)
            .ToList();
        return true;
    }

    private bool Save()
    {
        try
        {
            if (Entries.Count > MaxEntries || Entries.Any(entry => !Normalize(entry)))
                return false;

            var buffer = new StringBuilder();
            buffer.Append("format=").Append(SaveSchemaVersion).AppendLine();
            foreach (var entry in Entries)
                WriteEntry(buffer, entry);

            if (buffer.Length > MaxSaveLength)
                return false;

            using (var writer = _res.UserData.OpenWriteText(TemporarySavePath))
                writer.Write(buffer.ToString());

            if (_res.UserData.Exists(BackupSavePath))
                _res.UserData.Delete(BackupSavePath);
            if (_res.UserData.Exists(SavePath))
                _res.UserData.Rename(SavePath, BackupSavePath);

            _res.UserData.Rename(TemporarySavePath, SavePath);
            return true;
        }
        catch
        {
            if (!_res.UserData.Exists(SavePath) && _res.UserData.Exists(BackupSavePath))
                _res.UserData.Rename(BackupSavePath, SavePath);
            if (_res.UserData.Exists(TemporarySavePath))
                _res.UserData.Delete(TemporarySavePath);
            return false;
        }
    }

    private static void WriteEntry(StringBuilder buffer, CustomEmoteEntry entry)
    {
        buffer.AppendLine("[emote]");
        buffer.Append("name=").Append(Escape(entry.Name)).AppendLine();
        buffer.Append("custom=").Append(entry.Custom).AppendLine();
        buffer.Append("emoteId=").Append(Escape(entry.EmoteId ?? string.Empty)).AppendLine();
        buffer.Append("text=").Append(Escape(entry.Text ?? string.Empty)).AppendLine();
        buffer.Append("perspective=").Append(entry.Perspective).AppendLine();
        buffer.Append("range=").Append(entry.Range).AppendLine();
        buffer.Append("radius=").Append(entry.Radius).AppendLine();
        buffer.Append("showToGhosts=").Append(entry.ShowToGhosts).AppendLine();
        buffer.Append("soundId=").Append(Escape(entry.SoundId ?? string.Empty)).AppendLine();
        buffer.Append("bindKeys=").AppendJoin(',', entry.BindKeys).AppendLine();
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static string Escape(string value) => value
        .Replace("\\", "\\\\")
        .Replace("\n", "\\n")
        .Replace("\r", "\\r");

    private static string Unescape(string value)
    {
        var result = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i + 1 >= value.Length)
            {
                result.Append(value[i]);
                continue;
            }

            i++;
            result.Append(value[i] switch
            {
                'n' => '\n',
                'r' => '\r',
                _ => value[i],
            });
        }

        return result.ToString();
    }

}
