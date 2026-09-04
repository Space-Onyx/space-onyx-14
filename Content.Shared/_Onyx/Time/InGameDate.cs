using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Shared._Onyx.Time;

public static class InGameDate
{
    public static DateTime Today(IConfigurationManager configuration)
        => AddOffset(DateTime.Now.Date, configuration);

    public static DateTime Now(IConfigurationManager configuration)
        => AddOffset(DateTime.Now, configuration);

    private static DateTime AddOffset(DateTime date, IConfigurationManager configuration)
        => date.AddYears(Math.Clamp(configuration.GetCVar(CCVars.InGameYearOffset), 1 - date.Year, 9999 - date.Year));
}
