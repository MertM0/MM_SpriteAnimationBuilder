using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MM.SpriteAnimation
{
    internal static class PngFiles
    {
        private const string Extension = ".png";

        internal static IEnumerable<string> In(string directory)
        {
            if (!Directory.Exists(directory)) return Enumerable.Empty<string>();
            return Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                .Where(path => path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
                .Select(path => path.Replace('\\', '/'));
        }
    }
}
