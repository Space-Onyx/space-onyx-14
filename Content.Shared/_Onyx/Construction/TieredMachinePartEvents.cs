// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

namespace Content.Shared._Onyx.Construction;

using Robust.Shared.Utility;

public sealed class MachinePartsChangedEvent : EntityEventArgs
{
    public readonly IReadOnlyDictionary<MachinePartKind, float> Ratings;
    public readonly IReadOnlyDictionary<MachinePartKind, float> RatingSums;

    public MachinePartsChangedEvent(IReadOnlyDictionary<MachinePartKind, float> ratings,
        IReadOnlyDictionary<MachinePartKind, float> ratingSums)
    {
        Ratings = ratings;
        RatingSums = ratingSums;
    }

    public float GetRating(MachinePartKind kind)
    {
        return Ratings.GetValueOrDefault(kind, 1f);
    }

    public float GetRatingSum(MachinePartKind kind)
    {
        return RatingSums.GetValueOrDefault(kind);
    }

    public float GetTierBonusSum(MachinePartKind kind)
    {
        var rating = GetRating(kind);
        var sum = GetRatingSum(kind);
        return rating > 0f ? sum * (rating - 1f) / rating : 0f;
    }
}

public sealed class MachineUpgradeExamineEvent : EntityEventArgs
{
    private readonly FormattedMessage _message;

    public MachineUpgradeExamineEvent(FormattedMessage message)
    {
        _message = message;
    }

    public void Add(string name, float multiplier)
    {
        var percent = Math.Round(MathF.Abs(multiplier - 1f) * 100f, 2);
        var message = multiplier switch
        {
            < 1f => "machine-upgrade-decreased-by-percentage",
            > 1f => "machine-upgrade-increased-by-percentage",
            _ => "machine-upgrade-not-upgraded",
        };

        _message.TryAddMarkup(Loc.GetString(message,
            ("upgraded", Loc.GetString(name)),
            ("percent", percent)) + '\n', out _);
    }

    public void AddLine(string markup)
    {
        _message.TryAddMarkup(markup + '\n', out _);
    }
}
