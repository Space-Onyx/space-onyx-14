using Content.Shared._Onyx.Body;
using Robust.Client.Audio;
using Robust.Client.Player;
using Robust.Shared.Audio.Components;

namespace Content.Client._Onyx.Medical.Surgery;

public sealed partial class OrganHearingSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    private readonly Dictionary<EntityUid, float> _originalGains = new();

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(AudioSystem));
    }

    public override void Shutdown()
    {
        RestoreGains();
        base.Shutdown();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (_player.LocalEntity is not { } player || !HasComp<MissingEarsComponent>(player))
        {
            RestoreGains();
            return;
        }

        var query = EntityQueryEnumerator<AudioComponent>();
        while (query.MoveNext(out var uid, out var audio))
        {
            _originalGains.TryAdd(uid, audio.Gain);
            audio.Gain = 0f;
        }
    }

    private void RestoreGains()
    {
        foreach (var (uid, gain) in _originalGains)
            if (TryComp(uid, out AudioComponent? audio))
                audio.Gain = gain;

        _originalGains.Clear();
    }
}
