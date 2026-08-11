namespace Content.Shared.Silicons.Laws;

public partial class SiliconLaw
{
    public string GetDisplayString()
    {
        return Loc.TryGetString(LawString, out var localized) ? localized : LawString;
    }
}
