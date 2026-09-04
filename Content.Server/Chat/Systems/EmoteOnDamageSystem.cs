using System.Linq;
using Content.Shared.Damage.Systems;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Chat.Systems;

public sealed partial class EmoteOnDamageSystem : EntitySystem
{
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ChatSystem _chatSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmoteOnDamageComponent, DamageChangedEvent>(OnDamage);
    }

    private void OnDamage(EntityUid uid, EmoteOnDamageComponent emoteOnDamage, DamageChangedEvent args)
    {
        HandlePainDamageEmote(uid, emoteOnDamage, args); // <Onyx-PainSounds>

        if (!args.DamageIncreased)
            return;
    }

    /// <summary>
    /// Try to add an emote to the entity, which will be performed at an interval.
    /// </summary>
    public bool AddEmote(EntityUid uid, float threshold, string emotePrototypeId, EmoteOnDamageComponent? emoteOnDamage = null) // <Onyx-PainSounds-edited>
    {
        if (!Resolve(uid, ref emoteOnDamage, logMissing: false))
            return false;

        DebugTools.Assert(emoteOnDamage.LifeStage <= ComponentLifeStage.Running);
        DebugTools.Assert(ProtoMan.HasIndex<EmotePrototype>(emotePrototypeId), "Prototype not found. Did you make a typo?");

        if (!emoteOnDamage.EmotesThreshold.TryGetValue(threshold, out var emotes))
            return emoteOnDamage.EmotesThreshold.TryAdd(threshold, [emotePrototypeId]);
        return emotes.Add(emotePrototypeId);
    }

    public bool AddEmote(EntityUid uid, string emotePrototypeId, EmoteOnDamageComponent? component = null)
        => AddEmote(uid, 0f, emotePrototypeId, component);

    /// <summary>
    /// Stop preforming an emote. Note that by default this will queue empty components for removal.
    /// </summary>
    public bool RemoveEmote(EntityUid uid, float threshold, string emotePrototypeId, EmoteOnDamageComponent? emoteOnDamage = null, bool removeEmpty = true) // <Onyx-PainSounds-edited>
    {
        if (!Resolve(uid, ref emoteOnDamage, logMissing: false))
            return false;

        DebugTools.Assert(ProtoMan.HasIndex<EmotePrototype>(emotePrototypeId), "Prototype not found. Did you make a typo?");

        if (!emoteOnDamage.EmotesThreshold.TryGetValue(threshold, out var emotes) || !emotes.Remove(emotePrototypeId))
            return false;

        if (removeEmpty && emoteOnDamage.EmotesThreshold.Values.All(set => set.Count == 0))
            RemCompDeferred(uid, emoteOnDamage);

        return true;
    }

    public bool RemoveEmote(EntityUid uid, string emotePrototypeId, EmoteOnDamageComponent? component = null, bool removeEmpty = true)
        => RemoveEmote(uid, 0f, emotePrototypeId, component, removeEmpty);
}
