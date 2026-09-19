// Content taken from Goob-Station (https://github.com/Goob-Station/Goob-Station), licensed under AGPL-3.0-or-later.
//

using Content.Shared._Onyx.HeatTint;
using Content.Shared.Temperature;
using Content.Shared.Temperature.Components;

namespace Content.Server._Onyx.HeatTint;

public sealed partial class HeatTintSystem : SharedHeatTintSystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HeatTintComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<HeatTintComponent, TemperatureChangedEvent>(OnTemperatureChanged);
    }

    private void OnMapInit(Entity<HeatTintComponent> ent, ref MapInitEvent args)
    {
        if (TryComp<TemperatureComponent>(ent, out var temp))
            _appearance.SetData(ent, HeatTintVisuals.Temperature, temp.Temperature);
    }

    private void OnTemperatureChanged(Entity<HeatTintComponent> ent, ref TemperatureChangedEvent args)
    {
        _appearance.SetData(ent, HeatTintVisuals.Temperature, args.CurrentTemperature);
    }
}
