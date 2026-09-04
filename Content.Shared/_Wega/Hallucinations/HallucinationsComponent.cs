using Robust.Shared.Prototypes;

namespace Content.Shared.Hallucinations;

[RegisterComponent, Serializable]
public sealed partial class HallucinationsComponent : Component
{
    [DataField]
    public TimeSpan NextSecond = TimeSpan.Zero;

    /// <summary>
    /// How far from humanoid can appear hallucination
    /// </summary>
    [DataField]
    public float Range = 7f;

    /// <summary>
    /// How often (in seconds) hallucinations spawned
    /// </summary>
    [DataField]
    public float SpawnRate = 15f;

    /// <summary>
    /// Minimum spawn chance per humanoid
    /// </summary>
    [DataField]
    public float MinChance = 0.1f;

    /// <summary>
    /// Max spawn chance per humanoid
    /// </summary>
    [DataField]
    public float MaxChance = 0.8f;

    /// <summary>
    /// How much chance increased per spawn
    /// </summary>
    [DataField]
    public float IncreaseChance = 0.1f;

    /// <summary>
    /// Max spawned hallucinations count for one spawn
    /// </summary>
    [DataField]
    public int MaxSpawns = 5;

    /// <summary>
    /// How much entities already spawned
    /// </summary>
    public int SpawnedCount = 0;

    /// <summary>
    /// Current spawn chance
    /// </summary>
    [DataField]
    public float CurChance = 0.1f;

    /// <summary>
    ///     List of prototypes that are spawned as a hallucination.
    /// </summary>
    [DataField]
    public List<EntProtoId> Spawns = new();

    /// <summary>
    /// Hallucinations pack proto
    /// </summary>
    [DataField]
    public HallucinationsPrototype? Proto;

    /// <summary>
    /// Currently selected for hallucinations layer
    /// </summary>
    public int Layer = 50;
}
