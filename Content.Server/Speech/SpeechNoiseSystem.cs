using Content.Shared.Chat;
using Content.Shared._Onyx.Loudspeaker.Events; // <Onyx-Loudspeaker>
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Speech
{
    public sealed partial class SpeechSoundSystem : EntitySystem
    {
        [Dependency] private IGameTiming _gameTiming = default!;
        [Dependency] private IRobustRandom _random = default!;
        [Dependency] private SharedAudioSystem _audio = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SpeechComponent, EntitySpokeEvent>(OnEntitySpoke);
        }

        public SoundSpecifier? GetSpeechSound(Entity<SpeechComponent> ent, string message)
        {
            // <Onyx-Loudspeaker>
            var getSpeechSound = new GetSpeechSoundEvent();
            RaiseLocalEvent(ent, ref getSpeechSound);
            SpeechSoundsPrototype? prototype;
            if (getSpeechSound.Handled)
            {
                if (getSpeechSound.SpeechSoundProtoId is not { } protoId || !ProtoMan.TryIndex(protoId, out prototype))
                    return null;
            }
            else
            {
                if (ent.Comp.SpeechSounds == null)
                    return null;

                prototype = ProtoMan.Index<SpeechSoundsPrototype>(ent.Comp.SpeechSounds);
            }
            // </Onyx-Loudspeaker>

            if (prototype == null)
                return null;

            // Play speech sound
            SoundSpecifier? contextSound;

            // Different sounds for ask/exclaim based on last character
            contextSound = message[^1] switch
            {
                '?' => prototype.AskSound,
                '!' => prototype.ExclaimSound,
                _ => prototype.SaySound
            };

            // Use exclaim sound if most characters are uppercase.
            int uppercaseCount = 0;
            for (int i = 0; i < message.Length; i++)
            {
                if (char.IsUpper(message[i]))
                    uppercaseCount++;
            }
            if (uppercaseCount > (message.Length / 2))
            {
                contextSound = prototype.ExclaimSound;
            }

            var scale = (float) _random.NextGaussian(1, prototype.Variation);
            contextSound.Params = ent.Comp.AudioParams.WithPitchScale(scale);
            return contextSound;
        }

        private void OnEntitySpoke(EntityUid uid, SpeechComponent component, EntitySpokeEvent args)
        {
            var currentTime = _gameTiming.CurTime;
            var cooldown = TimeSpan.FromSeconds(component.SoundCooldownTime);

            // Ensure more than the cooldown time has passed since last speaking
            if (currentTime - component.LastTimeSoundPlayed < cooldown)
                return;

            var sound = GetSpeechSound((uid, component), args.Message);
            if (sound == null)
                return;

            component.LastTimeSoundPlayed = currentTime;
            _audio.PlayPvs(sound, uid);
        }
    }
}
