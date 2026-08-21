using Content.Shared._Onyx.Body;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    private bool CanHear(EntityUid entity)
    {
        return !HasComp<MissingEarsComponent>(entity);
    }

    // <Onyx-HearingVisibility>
    /// <summary>
    ///     Returns true if <paramref name="listener"/> can both hear <paramref name="source"/> and see it.
    ///     Chat from creatures on visibility layers the listener's eye cannot see is filtered out.
    /// </summary>
    private bool CanHearSource(EntityUid listener, EntityUid source)
    {
        return CanHear(listener) && CanSeeSource(listener, source);
    }

    /// <summary>
    ///     Returns true if <paramref name="listener"/> can see <paramref name="source"/>,
    ///     i.e. the source's cumulative visibility mask (from MetaData, incl. parents and the always-on bit)
    ///     is fully contained in the listener's effective visibility mask. Mirrors the engine's PVS check
    ///     where session VisMask = DefaultVisibilityMask | eye.VisibilityMask.
    /// </summary>
    private bool CanSeeSource(EntityUid listener, EntityUid source)
    {
        if (listener == source)
            return true;

        var sourceMask = Comp<MetaDataComponent>(source).VisibilityMask;
        var eyeMask = EyeComponent.DefaultVisibilityMask;
        if (TryComp(listener, out EyeComponent? eye))
            eyeMask |= eye.VisibilityMask;
        return (eyeMask & sourceMask) == sourceMask;
    }
    // </Onyx-HearingVisibility>
}
