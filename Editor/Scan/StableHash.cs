using System.IO;

namespace MM.SpriteAnimation
{
    internal struct StableHash
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        internal StableHash(ulong seed)
        {
            Value = seed;
        }

        internal ulong Value { get; private set; }

        internal static StableHash Create() => new StableHash(OffsetBasis);

        internal void Add(string text)
        {
            if (text != null)
            {
                foreach (char character in text) Mix(character);
            }
            Mix(0xFFFF);
        }

        internal void Add(long number)
        {
            for (int shift = 0; shift < 64; shift += 8) Mix((byte)(number >> shift));
        }

        internal void AddFileSize(string path)
        {
            var info = new FileInfo(path);
            Add(path);
            Add(info.Exists ? info.Length : -1L);
        }

        internal void AddFileContent(string path, ulong contentHash)
        {
            Add(path);
            Add((long)contentHash);
        }

        private void Mix(ulong value)
        {
            Value = (Value ^ value) * Prime;
        }
    }
}
