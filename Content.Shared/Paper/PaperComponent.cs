using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Content.Shared._Onyx.Paper; // <Onyx-PaperSignatures>

namespace Content.Shared.Paper;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PaperComponent : Component
{
    public PaperAction Mode;
    [DataField("content")] // <Onyx-PaperLanguages-edited>
    public string Content { get; set; } = "";

    [DataField("contentSize")]
    public int ContentSize { get; set; } = 10000;

    [DataField("stampedBy"), AutoNetworkedField]
    public List<StampDisplayInfo> StampedBy { get; set; } = new();

    // <Onyx-PaperSignatures>
    [DataField("signedBy"), AutoNetworkedField]
    public List<SignatureDisplayInfo> SignedBy { get; set; } = new();
    // </Onyx-PaperSignatures>

    /// <summary>
    ///     Stamp to be displayed on the paper, state from bureaucracy.rsi
    /// </summary>
    [DataField("stampState"), AutoNetworkedField]
    public string? StampState { get; set; }

    [DataField, AutoNetworkedField]
    public bool EditingDisabled;

    /// <summary>
    /// Sound played after writing to the paper.
    /// </summary>
    [DataField("sound")]
    public SoundSpecifier? Sound { get; private set; } = new SoundCollectionSpecifier("PaperScribbles", AudioParams.Default.WithVariation(0.1f));

    [Serializable, NetSerializable]
    public sealed class PaperBoundUserInterfaceState : BoundUserInterfaceState
    {
        public readonly string Text;
        public readonly List<StampDisplayInfo> StampedBy;
        public readonly List<SignatureDisplayInfo> SignedBy; // <Onyx-PaperSignatures>
        public readonly PaperAction Mode;

        public PaperBoundUserInterfaceState(string text, List<StampDisplayInfo> stampedBy, List<SignatureDisplayInfo> signedBy, PaperAction mode = PaperAction.Read) // <Onyx-PaperSignatures-edited>
        {
            Text = text;
            StampedBy = stampedBy;
            SignedBy = signedBy; // <Onyx-PaperSignatures>
            Mode = mode;
        }
    }

    [Serializable, NetSerializable]
    public sealed class PaperInputTextMessage : BoundUserInterfaceMessage
    {
        public readonly string Text;

        public PaperInputTextMessage(string text)
        {
            Text = text;
        }
    }

    [Serializable, NetSerializable]
    public enum PaperUiKey
    {
        Key
    }

    [Serializable, NetSerializable]
    public enum PaperAction
    {
        Read,
        Write,
    }

    [Serializable, NetSerializable]
    public enum PaperVisuals : byte
    {
        Status,
        Stamp
    }

    [Serializable, NetSerializable]
    public enum PaperStatus : byte
    {
        Blank,
        Written
    }
}
