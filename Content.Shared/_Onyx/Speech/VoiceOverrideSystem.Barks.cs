using Content.Shared.Speech.Components;
using Content.Shared._Onyx.SpeechBarks;

namespace Content.Shared.Speech.EntitySystems;

public sealed partial class VoiceOverrideSystem
{
    [SubscribeLocalEvent]
    private void OnTransformSpeakerBark(Entity<VoiceOverrideComponent> entity, ref TransformSpeakerBarkEvent args)
    {
        if (entity.Comp.Enabled)
            args.Data = entity.Comp.Bark ?? args.Data;
    }
}
