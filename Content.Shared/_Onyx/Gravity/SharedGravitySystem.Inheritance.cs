namespace Content.Shared.Gravity;

public abstract partial class SharedGravitySystem
{
    public bool GridOrMapHaveGravity(EntityUid gridUid)
    {
        var xform = Transform(gridUid);

        return _gravityQuery.TryComp(gridUid, out var gravity) && gravity.Enabled ||
               _gravityQuery.TryComp(xform.MapUid, out var mapGravity) && mapGravity.Enabled;
    }

    public bool IsAffectedByGravityChange(TransformComponent xform, EntityUid changedUid)
    {
        return xform.GridUid == changedUid || xform.MapUid == changedUid;
    }
}
