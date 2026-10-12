using System;
using System.Collections.Generic;
using System.Linq;

namespace MM.SpriteAnimation
{
    internal static class PartLayout
    {
        internal static IReadOnlyList<SpritePart> ActiveParts(SpriteAnimationProfile profile)
        {
            if (profile.MultiPart) return profile.Parts;
            return new[] { SpritePart.Single(profile.Parts.Count > 0 ? profile.Parts[0].RendererPath : string.Empty) };
        }

        internal static void Check(SpriteAnimationProfile profile, SpriteAnimationScan scan)
        {
            if (!profile.MultiPart) return;

            if (profile.Parts.Count == 0)
            {
                scan.AddError("Add at least one part.");
                return;
            }

            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (SpritePart part in profile.Parts)
            {
                if (part.Token.Length == 0) scan.AddError("Every part needs a token when Multi Part is on.");
                else if (!tokens.Add(part.Token)) scan.AddError($"Part '{part.Token}' is listed more than once.");
                if (!paths.Add(part.RendererPath)) scan.AddError($"Renderer path '{part.RendererPath}' is used by more than one part.");
            }
            if (profile.Parts[0].Optional) scan.AddError($"Part '{profile.Parts[0].Label}' sets the frame count and cannot be optional.");
        }

        internal static bool IsAnimationFolder(SpriteAnimationProfile profile, string folder)
        {
            if (!profile.MultiPart) return PngFiles.In(folder).Any();
            return profile.Parts.Any(part => part.Token.Length > 0 && System.IO.Directory.Exists($"{folder}/{part.Token}"));
        }

        internal static string Directory(string animationFolder, SpritePart part) => part.Token.Length == 0 ? animationFolder : $"{animationFolder}/{part.Token}";

        internal static string Stem(SpriteAnimationProfile profile, string animation, SpritePart part) => part.Token.Length == 0 ? $"{profile.FilePrefix}{animation}_" : $"{profile.FilePrefix}{animation}_{part.Token}_";
    }
}
