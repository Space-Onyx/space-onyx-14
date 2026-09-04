// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Server.Chat.Systems;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Genetics;
using Content.Shared.Jittering;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Genetics.System;

public sealed partial class EpilepsySystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedJitteringSystem _jitteringSystem = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IRobustRandom _random = default!;

    private static readonly ProtoId<EmotePrototype> Scream = "Scream";

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<EpilepsyGenComponent>();
        while (query.MoveNext(out var uid, out var epilepsy))
        {
            if (epilepsy.NextTimeTick <= 0)
            {
                epilepsy.NextTimeTick = 10;
                if (_random.Next(0, 100) < 1)
                {
                    _stun.TryUpdateParalyzeDuration(uid, TimeSpan.FromSeconds(15));
                    _jitteringSystem.DoJitter(uid, TimeSpan.FromSeconds(15), true);
                    _popup.PopupEntity(Loc.GetString("disease-epilepsy-massage"), uid, PopupType.Medium);
                    _chat.TryEmoteWithoutChat(uid, ProtoMan.Index(Scream), true);
                }
            }
            epilepsy.NextTimeTick -= frameTime;
        }
    }
}

