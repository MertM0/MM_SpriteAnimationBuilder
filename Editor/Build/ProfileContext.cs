using UnityEngine;

namespace MM.SpriteAnimation
{
    internal sealed class ProfileContext
    {
        internal SpriteAnimationScan Scan;
        internal ReferenceBlobs? Anchor;
        internal ReferenceBlobs? Tracked;
        internal Vector2 BodyPivot = new Vector2(0.5f, 0.5f);
    }
}
