// Content adapted from Goob-Station (https://github.com/Goob-Station/Goob-Station/pull/7076), licensed under AGPL-3.0-or-later.

using System.Numerics;
using Content.Server.Chat.Managers;
using Content.Server.Radio.EntitySystems;
using Content.Shared._GoobStation.Supermatter.Components;
using Content.Shared.Database;
using Content.Shared.Radio;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;

namespace Content.Server._GoobStation.Supermatter.Systems;

public sealed partial class SupermatterSystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private IChatManager _chatManager = default!;

    private void UpdateEventModifiers(SupermatterComponent sm)
    {
        if (sm.Surge && _gameTiming.CurTime.TotalMinutes >= sm.TimeLocked + sm.TimeToUnlock)
        {
            sm.Surge = false;
            _chatManager.SendAdminAlert($"SM variables unlocked at time {_gameTiming.CurTime.TotalMinutes}");
        }

        if (sm.Surge)
            return;

        AdjustSetpoints(sm);
    }

    private void TryRunEvent(EntityUid uid, SupermatterComponent sm)
    {
        sm.SMAngerValue = MathF.Max(sm.SMAngerValue, 0f);
        if (sm.SMAngerValue < sm.SMEventSetpoint)
            return;

        sm.SMAngerValue = 0f;
        var eventId = GetEventType(sm);
        var smEvent = _prototype.Index<SupermatterEventPrototype>(eventId);

        if (smEvent.Announcement is { } announcement)
        {
            var message = Loc.GetString(announcement);
            _radio.SendRadioMessage(uid, message, _prototype.Index<RadioChannelPrototype>(sm.RadioChannel), uid);
            _chatManager.SendAdminAlert($"{smEvent.ID} run by supermatter {uid}");
        }

        if (smEvent.GasToSpawn is { } gas)
        {
            _atmosphere.GetContainingMixture(uid, true, true)?.AdjustMoles(gas, 2000f);
            return;
        }

        if (smEvent.ProtoToSpawn is { } prototype)
        {
            Spawn(prototype, Transform(uid).Coordinates.Offset(-Vector2.UnitY));
            return;
        }

        if (smEvent.ID != "SMSurge")
            return;

        sm.TimeLocked = _gameTiming.CurTime.TotalMinutes;
        sm.GasEfficiencyFactorChanged = true;
        sm.GasEfficiency = 0.30f;
        sm.RadiationOutputFactorChanged = true;
        sm.RadiationOutputFactor = 0.06f;
        sm.Surge = true;
        _chatManager.SendAdminAlert($"Supermatter surge begun at time {_gameTiming.CurTime.TotalMinutes}");
    }

    private string GetEventType(SupermatterComponent sm)
    {
        var id = sm.SMLastAnger >= sm.HarshEventThreshold ? sm.HarshEvents : sm.NormalEvents;
        return _prototype.Index<WeightedRandomPrototype>(id).Pick(_rand);
    }

    private void AdjustSetpoints(SupermatterComponent sm)
    {
        if (sm.GasEfficiencyFactorChanged)
        {
            var oldValue = sm.GasEfficiency;
            sm.GasEfficiency = ApproachSetpoint(sm.GasEfficiency, sm.GasEfficiencySetpoint);
            _adminLog.Add(LogType.Supermatter,
                $"Supermatter gas efficiency factor adjusted by {sm.GasEfficiency - oldValue} to {sm.GasEfficiency}");
            sm.GasEfficiencyFactorChanged = sm.GasEfficiency != sm.GasEfficiencySetpoint;
        }

        if (!sm.RadiationOutputFactorChanged)
            return;

        var oldRadiation = sm.RadiationOutputFactor;
        sm.RadiationOutputFactor = ApproachSetpoint(sm.RadiationOutputFactor, sm.RadiationOutputFactorSetpoint);
        _adminLog.Add(LogType.Supermatter,
            $"Supermatter radiation output factor adjusted by {sm.RadiationOutputFactor - oldRadiation} to {sm.RadiationOutputFactor}");
        sm.RadiationOutputFactorChanged = sm.RadiationOutputFactor != sm.RadiationOutputFactorSetpoint;
    }

    private static float ApproachSetpoint(float value, float setpoint)
    {
        var difference = setpoint - value;
        return MathF.Abs(difference) <= 0.00001f ? setpoint : value + difference / 50f;
    }
}
