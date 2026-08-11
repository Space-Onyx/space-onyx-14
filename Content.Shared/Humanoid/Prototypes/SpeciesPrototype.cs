using Content.Shared.Body;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Dataset;
using Content.Shared.Humanoid.Markings;
using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Humanoid.Prototypes;

[Prototype]
public sealed partial class SpeciesPrototype : IPrototype
{
    /// <summary>
    /// Prototype ID of the species.
    /// </summary>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// User visible name of the species.
    /// </summary>
    [DataField(required: true)]
    public string Name { get; private set; } = default!;

    /// <summary>
    ///     Descriptor. Unused...? This is intended
    ///     for an eventual integration into IdentitySystem
    ///     (i.e., young human person, young lizard person, etc.)
    /// </summary>
    [DataField]
    public string Descriptor { get; private set; } = "humanoid";

    /// <summary>
    /// Whether the species is available "at round start" (In the character editor)
    /// </summary>
    [DataField(required: true)]
    public bool RoundStart { get; private set; } = false;

    // Corvax-Sponsors-Start
    /// <summary>
    /// Whether the species is available only for sponsors
    /// </summary>
    [DataField]
    public bool SponsorOnly { get; private set; } = false;
    // Corvax-Sponsors-End

    /// <summary>
    ///     Default skin tone for this species. This applies for non-human skin tones.
    /// </summary>
    [DataField]
    public Color DefaultSkinTone { get; private set; } = Color.White;

    /// <summary>
    ///     Default human skin tone for this species. This applies for human skin tones.
    ///     See <see cref="SkinColor.HumanSkinTone"/> for the valid range of skin tones.
    /// </summary>
    [DataField]
    public int DefaultHumanSkinTone { get; private set; } = 20;

    /// <summary>
    ///     Humanoid species variant used by this entity.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Prototype { get; private set; } = default!;

    /// <summary>
    /// Prototype used by the species for the dress-up doll in various menus.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId DollPrototype { get; private set; } = default!;

    /// <summary>
    /// Method of skin coloration used by the species.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<SkinColorationPrototype> SkinColoration { get; private set; }

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> MaleFirstNames { get; private set; } = "NamesFirstMale";

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> FemaleFirstNames { get; private set; } = "NamesFirstFemale";

    // Corvax-LastnameGender-Start: Split lastname field by gender
    [DataField]
    public ProtoId<LocalizedDatasetPrototype> MaleLastNames { get; private set; } = "NamesHumanLastMale";

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> FemaleLastNames { get; private set; } = "NamesHumanLastFemale";
    // Corvax-LastnameGender-End

    [DataField]
    public SpeciesNaming Naming { get; private set; } = SpeciesNaming.FirstLast;

    [DataField]
    public List<Sex> Sexes { get; private set; } = new() { Sex.Male, Sex.Female };

    /// <summary>
    ///     Emote sounds prototype conversion id for every sex. This is ALWAYS in the order: Male; Female; Unsexed.
    /// </summary>
    [DataField]
    public ProtoId<EmoteSoundsPrototype>[] DefaultSoundsBySex = ["MaleHuman", "FemaleHuman", "MaleHuman"];

    /// <summary>
    ///     List of user selectable voices in the menu. This should at least have the same sound banks as the defaults.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<EmoteSoundsPrototype>> Voices = ["MaleHuman", "FemaleHuman"];

    /// <summary>
    ///     Characters younger than this are too young to be hired by Nanotrasen.
    /// </summary>
    [DataField]
    public int MinAge = 18;

    /// <summary>
    ///     Characters younger than this appear young.
    /// </summary>
    [DataField]
    public int YoungAge = 30;

    /// <summary>
    ///     Characters older than this appear old. Characters in between young and old age appear middle aged.
    /// </summary>
    [DataField]
    public int OldAge = 60;

    /// <summary>
    ///     Characters cannot be older than this. Only used for restrictions...
    ///     although imagine if ghosts could age people WYCI...
    /// </summary>
    [DataField]
    public int MaxAge = 120;

    // <Onyx-HeightWidth>
    public const float ReferenceHeightCm = 175f;
    public const float ReferenceWeightKg = 65f;

    [DataField]
    public Vector2 BaseScale = Vector2.One;

    [DataField]
    public int DefaultHeightCm = 175;

    [DataField]
    public int DefaultWeightKg = 65;

    [DataField]
    public int MinHeightCm = 140;

    [DataField]
    public int MaxHeightCm = 195;

    [DataField]
    public int MinWeightKg = 50;

    [DataField]
    public int MaxWeightKg = 75;

    [DataField]
    public bool ScaleWidth = true;

    [DataField]
    public bool ScaleHeight = true;

    public float DefaultHeight => ClampHeight(HeightCmToScale(DefaultHeightCm));
    public float DefaultWidth => ClampWidth(WeightKgToScale(DefaultWeightKg));

    public (float Min, float Max) HeightRange => NormalizeRange(
        HeightCmToScale(MinHeightCm), HeightCmToScale(MaxHeightCm));

    public (float Min, float Max) WidthRange => NormalizeRange(
        WeightKgToScale(MinWeightKg), WeightKgToScale(MaxWeightKg));

    public float HeightCmToScale(float value) => value / (ReferenceHeightCm * SafeScale(BaseScale.Y));
    public float HeightScaleToCm(float value) => value * ReferenceHeightCm * SafeScale(BaseScale.Y);
    public float WeightKgToScale(float value) => value / (ReferenceWeightKg * SafeScale(BaseScale.X));
    public float WidthScaleToKg(float value) => value * ReferenceWeightKg * SafeScale(BaseScale.X);

    public float ClampHeight(float value) => Clamp(value, HeightRange, 1f);
    public float ClampWidth(float value) => Clamp(value, WidthRange, 1f);

    public Vector2 GetVisualScale(float height, float width)
    {
        return BaseScale * new Vector2(ScaleWidth ? ClampWidth(width) : 1f, ScaleHeight ? ClampHeight(height) : 1f);
    }

    private static float SafeScale(float value) => float.IsFinite(value) && value > 0f ? value : 1f;

    private static (float Min, float Max) NormalizeRange(float min, float max)
    {
        min = float.IsFinite(min) ? Math.Clamp(min, 0.5f, 2f) : 1f;
        max = float.IsFinite(max) ? Math.Clamp(max, 0.5f, 2f) : 1f;
        return min <= max ? (min, max) : (max, min);
    }

    private static float Clamp(float value, (float Min, float Max) range, float fallback)
    {
        return Math.Clamp(float.IsFinite(value) ? value : fallback, range.Min, range.Max);
    }
    // </Onyx-HeightWidth>
}

public enum SpeciesNaming : byte
{
    First,
    FirstLast,
    LastFirst, // <Onyx-Rodentia>
    FirstDashFirst,
    TheFirstofLast,
}
