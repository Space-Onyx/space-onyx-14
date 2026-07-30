namespace Content.Shared._Onyx.Salvage.Bosses;

public static class WeightedPicker
{
    public static T Pick<T>(IReadOnlyDictionary<T, float> weights, System.Random random) where T : notnull
    {
        var total = 0f;
        foreach (var weight in weights.Values)
            total += Math.Max(weight, 0f);
        var roll = random.NextDouble() * total;
        var fallback = default(T)!;
        foreach (var (item, weight) in weights)
        {
            fallback = item;
            roll -= Math.Max(weight, 0f);
            if (roll <= 0)
                return item;
        }

        return fallback;
    }
}
