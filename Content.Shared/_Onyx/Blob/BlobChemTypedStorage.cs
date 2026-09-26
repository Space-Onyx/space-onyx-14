// SPDX-FileCopyrightText: 2024 Aiden <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2024 Fishbait <Fishbait@git.ml>
// SPDX-FileCopyrightText: 2024 fishbait <gnesse@gmail.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections;
using System.Diagnostics.Contracts;
using Content.Shared._Onyx.Blob.Components;
using Content.Shared.Damage;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Blob;

#region BlobChemTypedStorage
[DataDefinition, Serializable, NetSerializable]
public abstract partial class BlobChemTypedStorage<T> : IEnumerable where T : notnull
{
    public abstract T BlazingOil { get; set; }
    public abstract T ReactiveSpines { get; set; }
    public abstract T RegenerativeMateria { get; set; }
    public abstract T ExplosiveLattice { get; set; }
    public abstract T ElectromagneticWeb { get; set; }
    public abstract T ComatoseFiber { get; set; }
    public abstract T ChainCoating { get; set; }
    public abstract T SinewyTendons { get; set; }
    public abstract T CorrosiveSlime { get; set; }
    public abstract T CryogenicPoison { get; set; }

    // Indexer to access fields via BlobChemType enumeration
    [Pure]
    public T this[BlobChemType type]
    {
        get => type switch
        {
            BlobChemType.BlazingOil => BlazingOil,
            BlobChemType.ReactiveSpines => ReactiveSpines,
            BlobChemType.RegenerativeMateria => RegenerativeMateria,
            BlobChemType.ExplosiveLattice => ExplosiveLattice,
            BlobChemType.ElectromagneticWeb => ElectromagneticWeb,
            BlobChemType.ComatoseFiber => ComatoseFiber,
            BlobChemType.ChainCoating => ChainCoating,
            BlobChemType.SinewyTendons => SinewyTendons,
            BlobChemType.CorrosiveSlime => CorrosiveSlime,
            BlobChemType.CryogenicPoison => CryogenicPoison,
            _ => throw new ArgumentOutOfRangeException(nameof(type), $"Unknown chemical type: {type}")
        };
        set
        {
            switch (type)
            {
                case BlobChemType.BlazingOil:
                    BlazingOil = value;
                    break;
                case BlobChemType.ReactiveSpines:
                    ReactiveSpines = value;
                    break;
                case BlobChemType.RegenerativeMateria:
                    RegenerativeMateria = value;
                    break;
                case BlobChemType.ExplosiveLattice:
                    ExplosiveLattice = value;
                    break;
                case BlobChemType.ElectromagneticWeb:
                    ElectromagneticWeb = value;
                    break;
                case BlobChemType.ComatoseFiber:
                    ComatoseFiber = value;
                    break;
                case BlobChemType.ChainCoating:
                    ChainCoating = value;
                    break;
                case BlobChemType.SinewyTendons:
                    SinewyTendons = value;
                    break;
                case BlobChemType.CorrosiveSlime:
                    CorrosiveSlime = value;
                    break;
                case BlobChemType.CryogenicPoison:
                    CryogenicPoison = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), $"Unknown chemical type: {type}");
            }
        }
    }

    // Method for adding a value
    public void Add(BlobChemType key, T value)
    {
        this[key] = value;
    }

    public bool ContainsKey(BlobChemType key)
    {
        return key is >= BlobChemType.BlazingOil and <= BlobChemType.CryogenicPoison;
    }

    // Realization IEnumerable
    public IEnumerator<KeyValuePair<BlobChemType, T>> GetEnumerator()
    {
        yield return new KeyValuePair<BlobChemType, T>(BlobChemType.BlazingOil, BlazingOil);
        yield return new KeyValuePair<BlobChemType, T>(BlobChemType.ReactiveSpines, ReactiveSpines);
        yield return new KeyValuePair<BlobChemType, T>(BlobChemType.RegenerativeMateria, RegenerativeMateria);
        yield return new KeyValuePair<BlobChemType, T>(BlobChemType.ExplosiveLattice, ExplosiveLattice);
        yield return new KeyValuePair<BlobChemType, T>(BlobChemType.ElectromagneticWeb, ElectromagneticWeb);
        yield return new KeyValuePair<BlobChemType, T>(BlobChemType.ComatoseFiber, ComatoseFiber);
        yield return new KeyValuePair<BlobChemType, T>(BlobChemType.ChainCoating, ChainCoating);
        yield return new KeyValuePair<BlobChemType, T>(BlobChemType.SinewyTendons, SinewyTendons);
        yield return new KeyValuePair<BlobChemType, T>(BlobChemType.CorrosiveSlime, CorrosiveSlime);
        yield return new KeyValuePair<BlobChemType, T>(BlobChemType.CryogenicPoison, CryogenicPoison);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
#endregion

[DataDefinition, Serializable, NetSerializable]
public sealed partial class BlobChemColors : BlobChemTypedStorage<Color>
{
    [DataField]
    public override Color BlazingOil { get; set; }
    [DataField]
    public override Color ReactiveSpines { get; set; }
    [DataField]
    public override Color RegenerativeMateria { get; set; }
    [DataField]
    public override Color ExplosiveLattice { get; set; }
    [DataField]
    public override Color ElectromagneticWeb { get; set; }
    [DataField]
    public override Color ComatoseFiber { get; set; }
    [DataField]
    public override Color ChainCoating { get; set; }
    [DataField]
    public override Color SinewyTendons { get; set; }
    [DataField]
    public override Color CorrosiveSlime { get; set; }
    [DataField]
    public override Color CryogenicPoison { get; set; }
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class BlobChemDamage : BlobChemTypedStorage<DamageSpecifier>
{
    [DataField]
    public override DamageSpecifier BlazingOil { get; set; } = new();
    [DataField]
    public override DamageSpecifier ReactiveSpines { get; set; } = new();
    [DataField]
    public override DamageSpecifier RegenerativeMateria { get; set; } = new();
    [DataField]
    public override DamageSpecifier ExplosiveLattice { get; set; } = new();
    [DataField]
    public override DamageSpecifier ElectromagneticWeb { get; set; } = new();
    [DataField]
    public override DamageSpecifier ComatoseFiber { get; set; } = new();
    [DataField]
    public override DamageSpecifier ChainCoating { get; set; } = new();
    [DataField]
    public override DamageSpecifier SinewyTendons { get; set; } = new();
    [DataField]
    public override DamageSpecifier CorrosiveSlime { get; set; } = new();
    [DataField]
    public override DamageSpecifier CryogenicPoison { get; set; } = new();
}
