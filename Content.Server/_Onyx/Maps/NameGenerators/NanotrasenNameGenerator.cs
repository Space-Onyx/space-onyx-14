using Robust.Shared.Random;

namespace Content.Server.Maps.NameGenerators;

public sealed partial class NanotrasenNameGenerator
{
    [DataField("prefix")]
    private string _stationPrefix = "NT";

    [DataField("suffix")]
    private string[] _suffixCodes = ["LV", "NS", "EV", "PR", "RX"];

    private string FormatExtendedName(string input)
    {
        var random = IoCManager.Resolve<IRobustRandom>();
        var number = random.Next(1, 1000);
        var suffix = $"{random.Pick(_suffixCodes)}-{random.Next(0, 1000):D3}";

        return input
            .Replace("{prefix}", _stationPrefix)
            .Replace("{prefixCreator}", PrefixCreator)
            .Replace("{suffix}", suffix)
            .Replace("{number}", number.ToString("D3"))
            .Replace("{0}", $"{_stationPrefix}{PrefixCreator}")
            .Replace("{1}", suffix);
    }
}
