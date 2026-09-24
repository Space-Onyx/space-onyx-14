// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Shared.Damage;

[TypeSerializer]
public sealed class DamageSpecifierDictionarySerializer :
    ITypeReader<Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>, MappingDataNode>
{
    public ValidationNode Validate(ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null)
    {
        var values = new Dictionary<ValidationNode, ValidationNode>();
        var prototypes = dependencies.Resolve<IPrototypeManager>();
        if (node.TryGet<MappingDataNode>("types", out var types))
            values.Add(new ValidatedValueNode(new ValueDataNode("types")),
                serializationManager.ValidateNode<Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>>(types, context));

        if (node.TryGet<MappingDataNode>("groups", out var groups))
            values.Add(new ValidatedValueNode(new ValueDataNode("groups")),
                serializationManager.ValidateNode<Dictionary<ProtoId<DamageGroupPrototype>, FixedPoint2>>(groups, context));

        foreach (var (key, value) in node.Children)
        {
            if (!prototypes.HasIndex<DamageTypePrototype>(key))
                continue;

            values.Add(new ValidatedValueNode(new ValueDataNode(key)),
                serializationManager.ValidateNode<FixedPoint2>(value, context));
        }

        return new ValidatedMappingNode(values);
    }

    public Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> Read(ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>>? instanceProvider = null)
    {
        var damage = instanceProvider?.Invoke() ?? new();
        var prototypes = dependencies.Resolve<IPrototypeManager>();
        if (node.TryGet<MappingDataNode>("types", out var types))
        {
            serializationManager.Read<Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>>(
                types, hookCtx, context, instanceProvider: () => damage, notNullableOverride: true);
        }

        foreach (var (key, value) in node.Children)
        {
            ProtoId<DamageTypePrototype> damageType = key;
            if (!prototypes.HasIndex(damageType))
                continue;

            damage[damageType] = serializationManager.Read<FixedPoint2>(value, hookCtx, context);
        }

        if (!node.TryGet<MappingDataNode>("groups", out var groups))
            return damage;

        var groupDamage = serializationManager.Read<Dictionary<ProtoId<DamageGroupPrototype>, FixedPoint2>>(
            groups, hookCtx, context, notNullableOverride: true);
        foreach (var (groupId, amount) in groupDamage)
        {
            if (!prototypes.TryIndex(groupId, out var group))
                continue;

            var remainingTypes = group.DamageTypes.Count;
            var remainingDamage = amount;
            foreach (var damageType in group.DamageTypes)
            {
                var distributed = remainingDamage / FixedPoint2.New(remainingTypes);
                damage[damageType] = damage.GetValueOrDefault(damageType) + distributed;
                remainingDamage -= distributed;
                remainingTypes--;
            }
        }

        return damage;
    }
}
