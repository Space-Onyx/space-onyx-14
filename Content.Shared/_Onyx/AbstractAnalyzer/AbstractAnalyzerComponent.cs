using Robust.Shared.Audio;

namespace Content.Shared._Onyx.AbstractAnalyzer;

public abstract partial class AbstractAnalyzerComponent : Component
{
    public abstract TimeSpan NextUpdate { get; set; }

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan ScanDelay = TimeSpan.FromSeconds(0.8);

    [DataField]
    public EntityUid? ScannedEntity;

    [DataField]
    public float? MaxScanRange = 2.5f;

    [DataField]
    public SoundSpecifier? ScanningBeginSound;

    [DataField]
    public SoundSpecifier ScanningEndSound = new SoundPathSpecifier("/Audio/Items/Medical/healthscanner.ogg");

    [DataField]
    public bool Silent;
}
