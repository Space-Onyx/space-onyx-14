using System.Linq;
using Content.Shared._Onyx.Atmos.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Examine;

namespace Content.Shared._Onyx.Atmos.EntitySystems;

public abstract partial class SharedGasDestroyerSystem : EntitySystem
{
    [Dependency] private SharedAtmosphereSystem _atmosphere = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GasDestroyerComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<GasDestroyerComponent, AnchorStateChangedEvent>(OnAnchorChanged);
    }

    private void OnAnchorChanged(Entity<GasDestroyerComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored || ent.Comp.DestroyerState == GasDestroyerState.Disabled)
            return;

        ent.Comp.DestroyerState = GasDestroyerState.Disabled;
        Dirty(ent);
    }

    private void OnExamine(Entity<GasDestroyerComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(GasDestroyerComponent)))
        {
            if (ent.Comp.DestroyAnyGas)
            {
                args.PushMarkup(Loc.GetString("gas-destroyer-destroys-any-text"));
            }
            else if (ent.Comp.ListDestroyGas is not null)
            {
                var gases = ent.Comp.ListDestroyGas
                    .Select(pair => $"{Loc.GetString(_atmosphere.GetGas(pair.Key).Name)} ({Math.Round(pair.Value * 100, 2)}%)");
                args.PushMarkup(Loc.GetString("gas-destroyer-destroys-text", ("gas", string.Join(", ", gases))));
            }
            else if (ent.Comp.DestroyGas is { } gas)
            {
                args.PushMarkup(Loc.GetString("gas-destroyer-destroys-text",
                    ("gas", Loc.GetString(_atmosphere.GetGas(gas).Name))));
            }

            args.PushText(Loc.GetString("gas-destroyer-amount-text", ("moles", $"{ent.Comp.DestroyAmount:0.#}")));

            if (ent.Comp.MinExternalAmount > 0f)
                args.PushText(Loc.GetString("gas-destroyer-moles-cutoff-text", ("moles", $"{ent.Comp.MinExternalAmount:0.#}")));

            if (ent.Comp.MinExternalPressure > 0f)
                args.PushText(Loc.GetString("gas-destroyer-pressure-cutoff-text", ("pressure", $"{ent.Comp.MinExternalPressure:0.#}")));

            args.AddMarkup(ent.Comp.DestroyerState switch
            {
                GasDestroyerState.Disabled => Loc.GetString("gas-destroyer-state-disabled-text"),
                GasDestroyerState.Idle => Loc.GetString("gas-destroyer-state-idle-text"),
                GasDestroyerState.Working => Loc.GetString("gas-destroyer-state-working-text"),
                _ => throw new ArgumentOutOfRangeException(),
            });
        }
    }
}
