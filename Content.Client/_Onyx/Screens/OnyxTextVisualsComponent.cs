using System.Numerics;
using System.Threading;
using Content.Shared._Onyx.Screens;
using Robust.Client.Graphics;

namespace Content.Client._Onyx.Screens;

[RegisterComponent]
[Access(typeof(OnyxTextVisualsSystem), typeof(OnyxTextRenderingOverlay))]
public sealed partial class OnyxTextVisualsComponent : Component
{
    [DataField(required: true)]
    public List<OnyxTextVisualsRow> Rows = new()
    {
        new() { Layer = OnyxTextScreenVisualLayers.Line1, Offset = new Vector2(1f / 32f, 5f / 32f) },
        new() { Layer = OnyxTextScreenVisualLayers.Line2, Offset = new Vector2(1f / 32f, 0f) },
    };

    [DataField]
    public TimeSpan MarqueeRate = TimeSpan.FromSeconds(0.045f);

    [DataField]
    public int MarqueeWidth = 24;

    [DataField]
    public int MarqueePadding = 8;

    public CancellationTokenSource? Token;
}

[DataDefinition]
public sealed partial class OnyxTextVisualsRow
{
    public IRenderTexture? Texture;

    [DataField]
    public string Text = string.Empty;

    [DataField]
    public Vector2 Offset;

    [DataField(required: true)]
    public Enum Layer = OnyxTextScreenVisualLayers.Line1;

    public bool Marquee;
}
