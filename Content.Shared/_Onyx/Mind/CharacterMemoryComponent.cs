namespace Content.Shared.Mind;

[RegisterComponent]
public sealed partial class CharacterMemoryComponent : Component
{
    [DataField]
    public HashSet<Memory> Memories = new();

    [ViewVariables]
    public IEnumerable<Memory> AllMemories => Memories;

    public void AddMemory(Memory memory)
    {
        Memories.Add(memory);
    }
}
