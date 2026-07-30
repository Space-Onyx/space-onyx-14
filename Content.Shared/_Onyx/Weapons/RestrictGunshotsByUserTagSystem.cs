// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Onyx.Weapons;

public sealed partial class RestrictGunshotsByUserTagSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private TagSystem _tags = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RestrictGunshotsByUserTagComponent, ShotAttemptedEvent>(OnShotAttempted);
    }

    private void OnShotAttempted(Entity<RestrictGunshotsByUserTagComponent> ent, ref ShotAttemptedEvent args)
    {
        if (_tags.HasAllTags(args.User, ent.Comp.Contains) && !_tags.HasAnyTag(args.User, ent.Comp.DoesntContain))
            return;

        var time = _timing.CurTime;
        if (ent.Comp.Messages.Count != 0 && time > ent.Comp.LastPopup + TimeSpan.FromSeconds(1))
        {
            ent.Comp.LastPopup = time;
            _popup.PopupEntity(Loc.GetString(_random.Pick(ent.Comp.Messages)), args.User, args.User);
        }

        args.Cancel();
    }
}
