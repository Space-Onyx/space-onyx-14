using Content.Shared.Access.Systems;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared._Onyx.Weapons;

public sealed partial class RestrictByIdSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private EmagSystem _emag = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RestrictByIdComponent, AttemptShootEvent>(OnShoot);
        SubscribeLocalEvent<RestrictByIdComponent, AttemptMeleeEvent>(OnMelee);
        SubscribeLocalEvent<RestrictByIdComponent, GotEmaggedEvent>(OnEmagged);
    }

    private void OnEmagged(Entity<RestrictByIdComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction) || !ent.Comp.IsEmaggable)
            return;
        args.Handled = true;
        args.Repeatable = false;
    }

    private void OnShoot(Entity<RestrictByIdComponent> ent, ref AttemptShootEvent args)
    {
        if (HasComp<EmaggedComponent>(ent) || !ent.Comp.RestrictRanged || _access.IsAllowed(args.User, ent))
            return;
        args.Cancelled = true;
        args.Message = Loc.GetString(ent.Comp.FailText);
    }

    private void OnMelee(Entity<RestrictByIdComponent> ent, ref AttemptMeleeEvent args)
    {
        if (HasComp<EmaggedComponent>(ent) || !ent.Comp.RestrictMelee || _access.IsAllowed(args.User, ent))
            return;
        args.Cancelled = true;
        args.Message = Loc.GetString(ent.Comp.FailText);
    }
}
