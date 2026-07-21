using System.Diagnostics.CodeAnalysis;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Shared._Onyx.Atmos.Components;
using Content.Shared._Onyx.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Robust.Server.GameObjects;

namespace Content.Server._Onyx.Atmos.EntitySystems;

public sealed partial class GasDestroyerSystem : SharedGasDestroyerSystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GasDestroyerComponent, AtmosDeviceUpdateEvent>(OnUpdate);
    }

    private void OnUpdate(Entity<GasDestroyerComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        var oldState = ent.Comp.DestroyerState;

        if (!GetEnvironment(ent, out var environment) || !Transform(ent).Anchored)
        {
            ent.Comp.DestroyerState = GasDestroyerState.Disabled;
        }
        else
        {
            var destroyed = DestroyGas(ent.Comp, environment, ent.Comp.DestroyAmount * args.dt);
            ent.Comp.DestroyerState = destroyed < Atmospherics.GasMinMoles
                ? GasDestroyerState.Idle
                : GasDestroyerState.Working;
        }

        if (ent.Comp.DestroyerState != oldState)
            Dirty(ent);
    }

    private bool GetEnvironment(Entity<GasDestroyerComponent> ent, [NotNullWhen(true)] out GasMixture? environment)
    {
        var xform = Transform(ent);
        var position = _transform.GetGridOrMapTilePosition(ent, xform);
        if (_atmosphere.IsTileSpace(xform.GridUid, xform.MapUid, position))
        {
            environment = null;
            return false;
        }

        environment = _atmosphere.GetContainingMixture((ent, xform), true, true);
        return environment != null;
    }

    private static float DestroyGas(GasDestroyerComponent destroyer, GasMixture environment, float targetAmount)
    {
        var amount = CapDestroyAmount(destroyer, targetAmount, environment);
        if (amount < Atmospherics.GasMinMoles)
            return 0f;

        if (destroyer.DestroyAnyGas)
            return environment.Remove(amount).TotalMoles;

        if (destroyer.ListDestroyGas is not null)
            return DestroyGasList(environment, destroyer.ListDestroyGas, amount);

        return destroyer.DestroyGas is { } gas ? DestroySingleGas(environment, gas, amount) : 0f;
    }

    private static float DestroyGasList(GasMixture environment, Dictionary<Gas, float> gases, float amount)
    {
        var destroyed = 0f;
        foreach (var (gas, coefficient) in gases)
        {
            var remaining = amount - destroyed;
            if (remaining < Atmospherics.GasMinMoles)
                break;

            destroyed += DestroySingleGas(environment, gas, MathF.Min(remaining, amount * coefficient));
        }

        return destroyed;
    }

    private static float DestroySingleGas(GasMixture environment, Gas gas, float amount)
    {
        var toDestroy = Math.Clamp(amount, 0f, environment.GetMoles(gas));
        if (toDestroy < Atmospherics.GasMinMoles)
            return 0f;

        environment.AdjustMoles(gas, -toDestroy);
        return toDestroy;
    }

    private static float CapDestroyAmount(GasDestroyerComponent destroyer, float targetAmount, GasMixture environment)
    {
        if (environment.TotalMoles <= destroyer.MinExternalAmount ||
            environment.Pressure <= destroyer.MinExternalPressure ||
            environment.Temperature <= 0f)
            return 0f;

        var pressureLimited = (environment.Pressure - destroyer.MinExternalPressure) * environment.Volume /
            (environment.Temperature * Atmospherics.R);
        var removable = Math.Min(pressureLimited, environment.TotalMoles - destroyer.MinExternalAmount);
        return Math.Clamp(removable, 0f, targetAmount);
    }
}
