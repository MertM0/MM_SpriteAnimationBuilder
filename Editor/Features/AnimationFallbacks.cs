using System;
using System.Collections.Generic;
using System.Linq;

namespace MM.SpriteAnimation
{
    internal static class AnimationFallbacks
    {
        internal static void Check(SpriteAnimationProfile profile, SpriteAnimationScan scan)
        {
            IReadOnlyList<AnimationFallback> rules = profile.Fallbacks;
            string suffix = profile.IsVariant ? $" (edit it on '{profile.VariantOf.DisplayName}')" : string.Empty;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var keys = new HashSet<string>(rules.Select(rule => rule.Animation), StringComparer.Ordinal);
            for (int i = 0; i < rules.Count; i++)
            {
                AnimationFallback rule = rules[i];
                if (rule.Animation.Length == 0 || rule.Source.Length == 0) { scan.AddWarning($"Fallback #{i + 1} needs both names.{suffix}"); continue; }
                if (rule.Animation == rule.Source) { scan.AddWarning($"Fallback '{rule.Animation}' points to itself.{suffix}"); continue; }
                if (!seen.Add(rule.Animation)) scan.AddWarning($"Fallback '{rule.Animation}' is listed more than once.{suffix}");
                if (keys.Contains(rule.Source)) scan.AddWarning($"Fallback '{rule.Animation}' → '{rule.Source}' chains; point it at an animation with its own art.{suffix}");
            }
        }

        internal static string SourceOf(SpriteAnimationProfile profile, string animation)
        {
            foreach (AnimationFallback rule in profile.Fallbacks)
            {
                if (string.Equals(rule.Animation, animation, StringComparison.Ordinal) && rule.Source.Length > 0) return rule.Source;
            }
            return null;
        }
    }
}
