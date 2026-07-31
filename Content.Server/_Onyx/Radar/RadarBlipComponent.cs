using Content.Shared._Onyx.Radar;

namespace Content.Server._Onyx.Radar;

[RegisterComponent]
public sealed partial class RadarBlipComponent : Component
{
    [DataField]
    public Color RadarColor = Color.Red;

    [DataField]
    public Color HighlightedRadarColor = Color.OrangeRed;

    [DataField]
    public float Scale = 1f;

    [DataField]
    public RadarBlipShape Shape = RadarBlipShape.Circle;

    [DataField]
    public bool RequireNoGrid;

    [DataField]
    public bool VisibleFromOtherGrids;

    [DataField]
    public bool Enabled = true;
}
