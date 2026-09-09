// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared._Onyx.Traits;
using Content.Shared.Atmos;
using Content.Shared.Body.Components;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Traits;

public sealed partial class SmellSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly TimeSpan SmellCooldown = TimeSpan.FromSeconds(30);
    private const float MinimumAmmoniaFraction = 0.01f;
    private readonly Dictionary<EntityUid, TimeSpan> _nextSmell = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RespiratorComponent, InhaledGasEvent>(OnInhaledGas);
        SubscribeLocalEvent<RespiratorComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<AnosmiaComponent, SmellAttemptEvent>(OnSmellAttempt);
    }

    private void OnInhaledGas(Entity<RespiratorComponent> ent, ref InhaledGasEvent args)
    {
        if (args.Gas.TotalMoles <= 0f ||
            args.Gas[(int) Gas.Ammonia] / args.Gas.TotalMoles < MinimumAmmoniaFraction ||
            _nextSmell.GetValueOrDefault(ent) > _timing.CurTime)
        {
            return;
        }

        var attempt = new SmellAttemptEvent();
        RaiseLocalEvent(ent.Owner, ref attempt);
        if (attempt.Cancelled)
            return;

        _nextSmell[ent] = _timing.CurTime + SmellCooldown;
        _popup.PopupEntity(Loc.GetString("ammonia-smell"), ent, ent);
    }

    private void OnSmellAttempt(Entity<AnosmiaComponent> ent, ref SmellAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnTerminating(Entity<RespiratorComponent> ent, ref EntityTerminatingEvent args)
    {
        _nextSmell.Remove(ent);
    }
}
