using Content.Shared._Onyx.Body;
using Robust.Client.Audio;
using Robust.Client.Player;
using Robust.Shared.Audio.Components;

namespace Content.Client._Onyx.Medical.Surgery;

public sealed partial class OrganHearingSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(AudioSystem));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (_player.LocalEntity is not { } player || !HasComp<MissingEarsComponent>(player))
            return;

        var query = EntityQueryEnumerator<AudioComponent>();
        while (query.MoveNext(out _, out var audio))
            audio.Gain = 0f;
    }
}
