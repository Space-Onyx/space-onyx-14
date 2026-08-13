// SPDX-FileCopyrightText: 2025 Piras314 <p1r4s@proton.me>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Onyx.Abductor;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RestrictInteractionByUserTagComponent : Component
{
    [DataField, AutoNetworkedField] public List<ProtoId<TagPrototype>> Contains = [];
    [DataField, AutoNetworkedField] public List<string> Messages = [];
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RestrictMeleeByUserTagComponent : Component
{
    [DataField, AutoNetworkedField] public List<ProtoId<TagPrototype>> Contains = [];
    [DataField, AutoNetworkedField] public List<string> Messages = [];
}

public sealed partial class AbductorRestrictionSystem : EntitySystem
{
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RestrictInteractionByUserTagComponent, BeforeRangedInteractEvent>(OnInteract);
        SubscribeLocalEvent<RestrictMeleeByUserTagComponent, AttemptMeleeEvent>(OnMelee);
    }

    private void OnInteract(Entity<RestrictInteractionByUserTagComponent> ent, ref BeforeRangedInteractEvent args)
    {
        if (_tags.HasAllTags(args.User, ent.Comp.Contains))
            return;

        if (ent.Comp.Messages.Count != 0)
            _popup.PopupClient(Loc.GetString(_random.Pick(ent.Comp.Messages)), args.User);
        args.Handled = true;
    }

    private void OnMelee(Entity<RestrictMeleeByUserTagComponent> ent, ref AttemptMeleeEvent args)
    {
        if (_tags.HasAllTags(args.User, ent.Comp.Contains))
            return;

        if (ent.Comp.Messages.Count != 0)
            args.Message = Loc.GetString(_random.Pick(ent.Comp.Messages));
        args.Cancelled = true;
    }
}
