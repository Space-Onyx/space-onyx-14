using Robust.Shared.Serialization;

namespace Content.Shared.PDA
{
    [Serializable, NetSerializable]
    public enum PdaVisuals
    {
        IdCardInserted,
        ScreenState, // <Onyx-PdaScreenVisuals>
        PdaType
    }

    [Serializable, NetSerializable]
    public enum PdaUiKey
    {
        Key
    }

}
