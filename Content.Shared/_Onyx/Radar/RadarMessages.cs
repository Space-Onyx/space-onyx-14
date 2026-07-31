using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Radar;

[Serializable, NetSerializable]
public enum RadarBlipShape
{
    Circle,
    Square,
    Triangle,
    Star,
    Diamond,
    Hexagon,
    Arrow,
    Ring,
}

[Serializable, NetSerializable]
public sealed class GiveBlipsEvent(
    List<(Vector2 Position, float Scale, Color Color, RadarBlipShape Shape)> blips,
    List<(Vector2 Start, Vector2 End, float Thickness, Color Color)> lines) : EntityEventArgs
{
    public readonly List<(Vector2 Position, float Scale, Color Color, RadarBlipShape Shape)> Blips = blips;
    public readonly List<(Vector2 Start, Vector2 End, float Thickness, Color Color)> Lines = lines;
}

[Serializable, NetSerializable]
public sealed class RequestBlipsEvent(NetEntity radar) : EntityEventArgs
{
    public readonly NetEntity Radar = radar;
}
