using Content.Server.Speech.Components;
using Content.Server.Speech.Prototypes;
using Content.Shared.Clothing;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server._Onyx.Speech;

[RegisterComponent]
public sealed partial class AddAccentClothingComponent : Component
{
    [DataField(required: true)]
    public string Accent = default!;

    [DataField("replacement", customTypeSerializer: typeof(PrototypeIdSerializer<ReplacementAccentPrototype>))]
    public string? ReplacementPrototype;

    public bool IsActive;
}

public sealed partial class AddAccentClothingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AddAccentClothingComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<AddAccentClothingComponent, ClothingGotUnequippedEvent>(OnUnequipped);
    }

    private void OnEquipped(Entity<AddAccentClothingComponent> entity, ref ClothingGotEquippedEvent args)
    {
        var type = Factory.GetRegistration(entity.Comp.Accent).Type;
        if (HasComp(args.Wearer, type))
            return;

        var accent = (Component) Factory.GetComponent(type);
        AddComp(args.Wearer, accent);
        if (accent is ReplacementAccentComponent replacement)
            replacement.Accent = entity.Comp.ReplacementPrototype!;

        entity.Comp.IsActive = true;
    }

    private void OnUnequipped(Entity<AddAccentClothingComponent> entity, ref ClothingGotUnequippedEvent args)
    {
        if (!entity.Comp.IsActive)
            return;

        RemComp(args.Wearer, Factory.GetRegistration(entity.Comp.Accent).Type);
        entity.Comp.IsActive = false;
    }
}
