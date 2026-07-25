using Content.Shared.Damage;

namespace Content.Shared._Onyx.Weather;

[RegisterComponent]
public sealed partial class WeatherDamageComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    [ViewVariables]
    public TimeSpan NextUpdate;
}
