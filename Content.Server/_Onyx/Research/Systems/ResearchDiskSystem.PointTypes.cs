using Content.Shared.Interaction;
using Content.Shared.Research.Components;
using Content.Shared._Onyx.Research;

namespace Content.Server.Research.Disk;

public sealed partial class ResearchDiskSystem
{
    /// <summary>
    /// Awards the disk's typed point rewards (or the legacy grant) and consumes the disk.
    /// </summary>
    private bool TryAwardDiskPoints(
        EntityUid uid,
        ResearchDiskComponent component,
        AfterInteractEvent args,
        ResearchServerComponent server)
    {
        if (args.Target is not { } target)
            return false;

        string popup;
        if (component.PointRewards is { Count: > 0 } rewards)
        {
            foreach (var reward in rewards)
            {
                _research.ModifyServerPoints(target, reward.Type, reward.Amount, server);
            }

            popup = Loc.GetString("research-disk-inserted-typed", ("points", _research.FormatPointAmounts(rewards)));
        }
        else
        {
            _research.ModifyServerPoints(target, component.Points, server);

            if (component.GrantExperimentalPoints)
                _research.ModifyServerPoints(target, "Experimental", component.Points, server);

            popup = Loc.GetString("research-disk-inserted", ("points", component.Points));
        }

        _popupSystem.PopupEntity(popup, target, args.User);
        QueueDel(uid);
        args.Handled = true;
        return true;
    }
}
