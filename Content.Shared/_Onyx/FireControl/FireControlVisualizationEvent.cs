using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.FireControl;

[Serializable, NetSerializable]
public sealed class FireControlVisualizationEvent : EntityEventArgs
{
    public NetEntity Entity { get; }
    public Dictionary<float, bool>? Directions { get; }
    public bool Enabled { get; }

    public FireControlVisualizationEvent(NetEntity entity, Dictionary<float, bool> directions)
    {
        Entity = entity;
        Directions = directions;
        Enabled = true;
    }

    public FireControlVisualizationEvent(NetEntity entity)
    {
        Entity = entity;
        Enabled = false;
    }
}
