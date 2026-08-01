using System.Diagnostics.CodeAnalysis;
using Content.Server._Onyx.Shuttles.Components;
using Content.Server.Shuttles.Components;
using Content.Shared._Onyx.Shuttles.Components;
using Content.Shared.Shuttles.Components;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    private bool TrySetupFTLDrive(
        EntityUid uid,
        ShuttleComponent shuttle,
        [NotNullWhen(true)] out FTLComponent? component)
    {
        if (!TrySetupFTL(uid, shuttle, out component))
            return false;

        EnsureComp<ActiveFTLDriveComponent>(uid).Data = CompOrNull<FTLDriveComponent>(uid)?.Data ?? FTLDriveComponent.DefaultData;
        return true;
    }

    private (float Startup, float Travel) GetFTLDriveTimes(EntityUid uid, float? startupTime, float? travelTime)
    {
        var data = GetActiveFTLDrive(uid);
        return (
            startupTime ?? data.StartupTime ?? DefaultStartupTime,
            travelTime ?? data.TravelTime ?? DefaultTravelTime);
    }

    private FTLDriveData GetActiveFTLDrive(EntityUid uid)
    {
        return CompOrNull<ActiveFTLDriveComponent>(uid)?.Data ?? FTLDriveComponent.DefaultData;
    }

    private bool HasPoweredFTLDrive(EntityUid uid)
    {
        return TryComp<FTLDriveComponent>(uid, out var drive) && drive.Data != FTLDriveComponent.DefaultData;
    }

    private TimeSpan GetFTLKnockdownTime(EntityUid? uid)
    {
        if (uid == null)
            return _hyperspaceKnockdownTime;

        return GetActiveFTLDrive(uid.Value).KnockdownTime is { } seconds
            ? TimeSpan.FromSeconds(seconds)
            : _hyperspaceKnockdownTime;
    }

    private float GetFTLArrivalTime(EntityUid uid)
    {
        return GetActiveFTLDrive(uid).ArrivalTime ?? DefaultArrivalTime;
    }

    private TimeSpan GetFTLCooldown(EntityUid uid)
    {
        if (GetActiveFTLDrive(uid).CooldownTime is { } seconds)
            return TimeSpan.FromSeconds(seconds);

        return HasComp<ArrivalsShuttleComponent>(uid) ? ArrivalsFTLCooldown : FTLCooldown;
    }

    private void ClearActiveFTLDrive(EntityUid uid)
    {
        RemCompDeferred<ActiveFTLDriveComponent>(uid);
    }

}
