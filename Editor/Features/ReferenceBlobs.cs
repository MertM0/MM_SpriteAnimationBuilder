using UnityEngine;

namespace MM.SpriteAnimation
{
    internal readonly struct ReferenceBlobs
    {
        internal static readonly ReferenceBlobs Unreadable = new ReferenceBlobs(-1, Vector2.zero, Vector2.zero, Vector2Int.zero);

        internal readonly int BlobCount;
        internal readonly Vector2 Left;
        internal readonly Vector2 Right;
        internal readonly Vector2Int CanvasSize;

        internal ReferenceBlobs(int blobCount, Vector2 left, Vector2 right, Vector2Int canvasSize)
        {
            BlobCount = blobCount;
            Left = left;
            Right = right;
            CanvasSize = canvasSize;
        }

        internal bool IsEmpty => BlobCount == 0;
        internal bool IsPair => BlobCount == 2;
        internal Vector2 Midpoint => (Left + Right) * 0.5f;
        internal float Angle => Mathf.Atan2(Right.y - Left.y, Right.x - Left.x) * Mathf.Rad2Deg;
    }
}
