using Content.Shared._Onyx.Medical.Surgery;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Onyx.Medical.Surgery;

public sealed partial class SurgeryInfectionSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> CapTag = "SurgerySterileCap";
    private static readonly ProtoId<TagPrototype> GlovesTag = "SurgerySterileGloves";
    private static readonly ProtoId<TagPrototype> MaskTag = "SurgerySterileMask";
    private static readonly ProtoId<DamageTypePrototype> Poison = "Poison";

    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private TagSystem _tags = default!;

    public void OnStep(ref SurgeryStepEvent args)
    {
        var protection = HasTaggedItem(args.User, "head", CapTag) ? 1 : 0;
        protection += HasTaggedItem(args.User, "gloves", GlovesTag) ? 1 : 0;
        protection += HasTaggedItem(args.User, "mask", MaskTag) ? 1 : 0;

        var damage = protection switch
        {
            0 => _random.Next(4, 9),
            1 => _random.Next(3, 7),
            2 => _random.Next(1, 4),
            _ => 0,
        };

        if (damage > 0)
            _damage.TryChangeDamage(args.Part,
                new DamageSpecifier(_prototypes.Index(Poison), damage),
                true,
                origin: args.User);
    }

    private bool HasTaggedItem(EntityUid user, string slot, ProtoId<TagPrototype> tag)
    {
        return _inventory.TryGetSlotEntity(user, slot, out var item) && _tags.HasTag(item.Value, tag);
    }
}
