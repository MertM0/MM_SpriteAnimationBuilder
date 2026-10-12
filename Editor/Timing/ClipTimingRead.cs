using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace MM.SpriteAnimation
{
    internal sealed class ClipTimingRead
    {
        internal ClipTimingRead(ClipTiming timing, IReadOnlyList<string> framePaths, string problem, IReadOnlyList<string> warnings)
        {
            Timing = timing;
            FramePaths = framePaths;
            Problem = problem;
            Warnings = warnings;
        }

        internal ClipTiming Timing { get; }
        internal IReadOnlyList<string> FramePaths { get; }
        internal string Problem { get; }
        internal IReadOnlyList<string> Warnings { get; }

        internal bool Matches(IReadOnlyList<string> frames)
        {
            if (frames == null || frames.Count != FramePaths.Count) return false;
            for (int i = 0; i < frames.Count; i++)
            {
                if (!string.Equals(frames[i], FramePaths[i], StringComparison.Ordinal)) return false;
            }
            return true;
        }

        internal bool IsInFileOrder()
        {
            int previous = int.MinValue;
            foreach (string path in FramePaths)
            {
                if (!TryFrameNumber(path, out int number) || number <= previous) return false;
                previous = number;
            }
            return true;
        }

        internal int[] FrameMapTo(IReadOnlyList<string> frames)
        {
            var map = new int[FramePaths.Count];
            for (int i = 0; i < map.Length; i++)
            {
                map[i] = -1;
                if (FramePaths[i] == null || frames == null) continue;
                for (int j = 0; j < frames.Count; j++)
                {
                    if (!string.Equals(frames[j], FramePaths[i], StringComparison.Ordinal)) continue;
                    map[i] = j;
                    break;
                }
            }
            return map;
        }

        internal static bool IsIdentity(int[] map, int frameCount)
        {
            if (map == null) return true;
            if (map.Length != frameCount) return false;
            for (int i = 0; i < map.Length; i++)
            {
                if (map[i] != i) return false;
            }
            return true;
        }

        private static bool TryFrameNumber(string path, out int number)
        {
            number = 0;
            if (string.IsNullOrEmpty(path)) return false;

            string name = Path.GetFileNameWithoutExtension(path);
            int separator = name.LastIndexOf('_');
            return separator >= 0 && int.TryParse(name.Substring(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out number);
        }
    }
}
