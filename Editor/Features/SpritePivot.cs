using UnityEditor;
using UnityEngine;

namespace MM.SpriteAnimation
{
    internal static class SpritePivot
    {
        private static readonly Vector2 CenterPivot = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 BottomPivot = new Vector2(0.5f, 0f);

        internal static void Check(SpriteAnimationProfile profile, SpriteAnimationScan scan)
        {
            if (profile.Pivot == SpritePivotMode.Anchor && profile.AnchorSprite == null) scan.AddError("Pivot is Anchor but Anchor Sprite is not assigned.");
        }

        internal static bool MeasureAnchor(SpriteAnimationProfile profile, SpriteAnimationReport report, out ReferenceBlobs? anchor)
        {
            anchor = null;
            if (profile.Pivot != SpritePivotMode.Anchor) return true;
            if (!ReferencePoseAnalyzer.TryMeasurePair(profile.AnchorSprite, report, out ReferenceBlobs blobs)) return false;

            anchor = blobs;
            return true;
        }

        internal static (SpriteAlignment Alignment, Vector2 Pivot) Resolve(SpriteAnimationProfile profile, ReferenceBlobs? anchor)
        {
            switch (profile.Pivot)
            {
                case SpritePivotMode.Bottom: return (SpriteAlignment.BottomCenter, BottomPivot);
                case SpritePivotMode.Custom: return (SpriteAlignment.Custom, profile.CustomPivot);
                case SpritePivotMode.Anchor: return (SpriteAlignment.Custom, anchor.HasValue ? ReferencePoseAnalyzer.NormalizedPivot(anchor.Value) : CenterPivot);
                default: return (SpriteAlignment.Center, CenterPivot);
            }
        }

        internal static Vector2 Normalized(SpriteAnimationProfile profile, ReferenceBlobs? anchor) => Resolve(profile, anchor).Pivot;

        internal static string ImportPath(SpriteAnimationProfile profile) => profile.Pivot == SpritePivotMode.Anchor ? AssetDatabase.GetAssetPath(profile.AnchorSprite) : null;
    }
}
