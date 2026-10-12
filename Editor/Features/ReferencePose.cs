using UnityEngine;

namespace MM.SpriteAnimation
{
    internal readonly struct ReferencePose
    {
        internal readonly bool Visible;
        internal readonly Vector2 Position;
        internal readonly float Angle;

        internal ReferencePose(bool visible, Vector2 position, float angle)
        {
            Visible = visible;
            Position = position;
            Angle = angle;
        }
    }
}
