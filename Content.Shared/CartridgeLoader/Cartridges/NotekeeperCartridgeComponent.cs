using Robust.Shared.GameStates;

namespace Content.Shared.CartridgeLoader.Cartridges;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NotekeeperCartridgeComponent : Component
{
    /// <summary>
    /// The list of notes that got written down
    /// </summary>
    // <Onyx-Notekeeper>
    [DataField, AutoNetworkedField]
    public List<NoteData> Notes = new();

    /// <summary>
    /// Next note ID for unique identification
    /// </summary>
    [DataField, AutoNetworkedField]
    public int NextNoteId = 1;

    /// <summary>
    /// Currently editing note ID
    /// </summary>
    [DataField, AutoNetworkedField]
    public int? EditingNoteId = null;

    /// <summary>
    /// Currently viewing note ID
    /// </summary>
    [DataField, AutoNetworkedField]
    public int? ViewingNoteId = null;
    // </Onyx-Notekeeper>
}
