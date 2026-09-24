// Content adapted from Goob Station (https://github.com/Goob-Station/Goob-Station), licensed under MIT.

namespace Content.Shared.Roles;

public sealed partial class JobPrototype
{
    [DataField]
    public bool CanBeAntag { get; private set; } = true;
}
