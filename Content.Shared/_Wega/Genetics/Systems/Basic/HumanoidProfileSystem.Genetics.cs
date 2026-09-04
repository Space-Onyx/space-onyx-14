#pragma warning disable IDE0130
namespace Content.Shared.Humanoid;

public sealed partial class HumanoidProfileSystem
{
    public void SetHeight(Entity<HumanoidProfileComponent> ent, float height)
    {
        ent.Comp.Height = height;
        Dirty(ent);
    }
}
