namespace Content.Server._Onyx.Singularity;

public sealed class ContainmentFieldIgnoreSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<ContainmentFieldIgnoreComponent, ContainmentFieldThrowEvent>(OnThrow);
    }

    private static void OnThrow(Entity<ContainmentFieldIgnoreComponent> entity, ref ContainmentFieldThrowEvent args)
    {
        args.Cancelled = true;
    }
}
