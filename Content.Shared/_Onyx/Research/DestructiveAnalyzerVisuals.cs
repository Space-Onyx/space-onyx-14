using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Research;

[Serializable, NetSerializable]
public enum DestructiveAnalyzerVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum DestructiveAnalyzerVisualState : byte
{
    Idle,
    Inserting,
    Loaded,
    Deconstructing,
}

[Serializable, NetSerializable]
public enum DestructiveAnalyzerVisualLayers : byte
{
    Base,
}
