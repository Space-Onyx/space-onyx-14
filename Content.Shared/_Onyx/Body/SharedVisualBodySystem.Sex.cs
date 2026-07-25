using Content.Shared.Humanoid;

namespace Content.Shared.Body;

public abstract partial class SharedVisualBodySystem
{
    public void SetSex(Entity<VisualBodyComponent?> entity, Sex sex)
    {
        if (!TryGatherMarkingsData(entity, null, out var profiles, out _, out _))
            return;

        foreach (var category in profiles.Keys)
            profiles[category] = profiles[category] with { Sex = sex };

        ApplyProfiles(entity.Owner, profiles);
    }
}
