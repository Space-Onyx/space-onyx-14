using Content.Shared.Speech;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Loudspeaker.Events;

[ByRefEvent]
public record struct GetLoudspeakerEvent(List<EntityUid>? Loudspeakers = null);

[ByRefEvent]
public record struct GetLoudspeakerDataEvent(
    bool IsActive = false,
    int? FontSize = null,
    bool AffectRadio = false,
    bool AffectChat = false,
    ProtoId<SpeechSoundsPrototype>? SpeechSounds = null);

[ByRefEvent]
public record struct GetSpeechSoundEvent(string? SpeechSoundProtoId = null, bool Handled = false);
