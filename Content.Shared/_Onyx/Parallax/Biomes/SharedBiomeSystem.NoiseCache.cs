using System.Collections.Concurrent;
using Robust.Shared.Noise;

namespace Content.Shared.Parallax.Biomes;

public abstract partial class SharedBiomeSystem
{
    private readonly ConcurrentDictionary<(FastNoiseLite Noise, int Seed), FastNoiseLite> _seededNoise = new();

    private FastNoiseLite GetSeededNoise(FastNoiseLite source, int seed)
    {
        return _seededNoise.GetOrAdd((source, seed), static (key, manager) =>
        {
            var noise = new FastNoiseLite();
            manager.CopyTo(key.Noise, ref noise, notNullableOverride: true);
            noise.SetSeed(noise.GetSeed() + key.Seed);
            noise.SetFractalOctaves(noise.GetFractalOctaves());
            return noise;
        }, _serManager);
    }
}
