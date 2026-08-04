using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Language;

[Serializable, NetSerializable]
public sealed class SetLanguageMessage(ProtoId<LanguagePrototype> language) : EntityEventArgs
{
    public ProtoId<LanguagePrototype> Language = language;
}

public sealed class CollectLanguageKnowledgeEvent : EntityEventArgs
{
    public readonly HashSet<ProtoId<LanguagePrototype>> SpokenLanguages = new();
    public readonly HashSet<ProtoId<LanguagePrototype>> UnderstoodLanguages = new();
    public bool UnderstandsAllLanguages;
}
