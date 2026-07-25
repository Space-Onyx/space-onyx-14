using Content.Shared.EntityEffects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Onyx.EntityEffects.Effects.Audio;

public sealed partial class PlaySoundEntityEffectSystem : EntityEffectSystem<TransformComponent, PlaySound>
{
    [Dependency] private SharedAudioSystem _audio = default!;

    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<PlaySound> args)
    {
        _audio.PlayPredicted(args.Effect.Sound, entity.Owner, args.User);
    }
}

public sealed partial class PlaySound : EntityEffectBase<PlaySound>
{
    [DataField(required: true)]
    public SoundSpecifier Sound = default!;
}
