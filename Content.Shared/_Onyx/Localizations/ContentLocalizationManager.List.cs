namespace Content.Shared.Localizations;

public sealed partial class ContentLocalizationManager
{
    public static string FormatListLocalized(List<string> list, string conjunctionLocKey)
    {
        var conjunction = Loc.GetString(conjunctionLocKey);
        return list.Count switch
        {
            0 => string.Empty,
            1 => list[0],
            2 => $"{list[0]} {conjunction} {list[1]}",
            _ => $"{string.Join(", ", list.GetRange(0, list.Count - 1))}, {conjunction} {list[^1]}",
        };
    }
}
