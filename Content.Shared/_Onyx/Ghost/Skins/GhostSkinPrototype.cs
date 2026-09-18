// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Diagnostics.CodeAnalysis;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.Ghost.Skins;

/// <summary>
/// A selectable ghost appearance ("skin") shown in the lobby ghost skins menu.
/// Skins are pure visuals applied on top of any observer entity,
/// using <see cref="JobRequirement"/> for playtime unlocks.
/// </summary>
[Prototype]
public sealed partial class GhostSkinPrototype : IPrototype, IInheritingPrototype
{
    public const string DefaultSkinId = "Default";

    [IdDataField]
    public string ID { get; private set; } = default!;

    [NeverPushInheritance, AbstractDataField]
    public bool Abstract { get; private set; }

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<GhostSkinPrototype>))]
    public string[]? Parents { get; private set; }

    /// <summary>
    /// Menu grouping, e.g. Color, Misc, Personal.
    /// </summary>
    [DataField]
    public string Category { get; private set; } = "Misc";

    /// <summary>
    /// RSI applied to the observer base layer. Null means the vanilla look.
    /// </summary>
    [DataField]
    public ResPath? Sprite { get; private set; }

    /// <summary>
    /// RSI state applied to the observer base layer. Null means the vanilla look.
    /// </summary>
    [DataField]
    public string? State { get; private set; }

    /// <summary>
    /// Tint applied to the observer base layer. Null keeps the entity tint.
    /// </summary>
    [DataField]
    public Color? Color { get; private set; }

    /// <summary>
    /// Optional ckey lock. When set, the skin is invisible to everyone else.
    /// </summary>
    [DataField]
    public List<string>? Ckeys { get; private set; }

    /// <summary>
    /// Playtime and other unlock conditions. Reuses the shared job requirement system.
    /// </summary>
    [DataField]
    public List<JobRequirement>? Requirements { get; private set; }

    /// <summary>
    /// Optional explicit locale id. Defaults to "ghost-skin-{id}-name".
    /// </summary>
    [DataField]
    public string? Name { get; private set; }

    public string DisplayName => Loc.GetString(Name ?? $"ghost-skin-{ID.ToLowerInvariant()}-name");

    /// <summary>
    /// Optional explicit locale id. Defaults to "ghost-skin-{id}-desc".
    /// </summary>
    [DataField]
    public string? Description { get; private set; }

    public string DisplayDesc => Loc.GetString(Description ?? $"ghost-skin-{ID.ToLowerInvariant()}-desc");

    public string DisplayCategory => Loc.GetString($"ghost-skin-category-{Category.ToLowerInvariant()}");

    public bool CanUse(
        ICommonSession session,
        [NotNullWhen(false)] out FormattedMessage? failReason,
        out bool canSee)
    {
        canSee = true;
        failReason = null;

        if (Ckeys is { Count: > 0 })
        {
            var name = NormalizeCkey(session.Name);
            var allowed = false;
            foreach (var ckey in Ckeys)
            {
                if (string.Equals(NormalizeCkey(ckey), name, StringComparison.OrdinalIgnoreCase))
                {
                    allowed = true;
                    break;
                }
            }

            if (!allowed)
            {
                canSee = false;
                failReason = FormattedMessage.FromMarkupPermissive(
                    Loc.GetString("ghost-skin-fail-exclusive"));
                return false;
            }
        }

        if (Requirements is null)
            return true;

        var playtime = IoCManager.Resolve<ISharedPlaytimeManager>();
        var proto = IoCManager.Resolve<IPrototypeManager>();
        var entMan = IoCManager.Resolve<IEntityManager>();
        var playTimes = playtime.GetPlayTimes(session);

        var result = true;
        foreach (var requirement in Requirements)
        {
            if (requirement.Check(entMan, proto, null, playTimes, out var reason))
                continue;

            result = false;
            failReason = failReason is null
                ? reason
                : FormattedMessage.FromMarkupPermissive($"{failReason.ToMarkup()}\n{reason?.ToMarkup()}");
        }

        return result;
    }

    private static string NormalizeCkey(string ckey)
    {
        // Local sessions show up as "localhost@Name".
        if (ckey.StartsWith("localhost@", StringComparison.OrdinalIgnoreCase))
            ckey = ckey.Substring("localhost@".Length);

        return ckey.Trim();
    }
}
