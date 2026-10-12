using System;
using System.Collections.Generic;
using System.IO;

namespace MM.SpriteAnimation
{
    internal sealed class FileStampCache<T>
    {
        private readonly Dictionary<string, (long Length, long Ticks, T Value)> entries = new Dictionary<string, (long Length, long Ticks, T Value)>(StringComparer.Ordinal);
        private readonly Func<string, T> compute;

        internal FileStampCache(Func<string, T> compute)
        {
            this.compute = compute;
        }

        internal T Get(string path)
        {
            var info = new FileInfo(path);
            if (!info.Exists) throw new FileNotFoundException($"'{path}' does not exist.", path);

            long ticks = info.LastWriteTimeUtc.Ticks;
            if (entries.TryGetValue(path, out (long Length, long Ticks, T Value) cached) && cached.Length == info.Length && cached.Ticks == ticks) return cached.Value;

            T value = compute(path);
            entries[path] = (info.Length, ticks, value);
            return value;
        }
    }
}
