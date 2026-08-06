/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared.Popups;
using Content.Shared.Standing;

namespace Content.Shared._Onyx.ZLevels.Damage.SoftPaws;

public sealed partial class CESoftPawsSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StandingStateSystem _standingState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CESoftPawsComponent, CEZFallingDamageCalculateEvent>(OnFallingDamageCalculate);
    }

    private void OnFallingDamageCalculate(Entity<CESoftPawsComponent> ent, ref CEZFallingDamageCalculateEvent args)
    {
        // Event also fires on landed-on entities; soft paws only apply to the faller's own legs.
        if (ent.Owner != args.Fallen)
            return;

        if (_standingState.IsDown(ent.Owner))
            return;

        if (args.Speed <= ent.Comp.MaxSpeedLimit)
        {
            args.DamageMultiplier *= ent.Comp.DamageMultiplier;
            args.StunMultiplier *= ent.Comp.StunMultiplier;

            _popup.PopupPredicted(Loc.GetString("ce-soft-paws"), ent, ent);
        }
        else
        {
            args.DamageMultiplier *= ent.Comp.DamageHardFallMultiplier;
            args.StunMultiplier *= ent.Comp.StunHardFallMultiplier;

            _popup.PopupPredicted(Loc.GetString("ce-soft-paws-too-high"), ent, ent, PopupType.SmallCaution);
        }
    }
}
