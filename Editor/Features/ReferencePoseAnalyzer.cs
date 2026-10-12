using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MM.SpriteAnimation
{
    internal static class ReferencePoseAnalyzer
    {
        private const byte AlphaThreshold = 128;
        private const int MinBlobPixels = 20;

        private static readonly FileStampCache<ReferenceBlobs> Cache = new FileStampCache<ReferenceBlobs>(Measure);

        internal static ReferenceBlobs MeasureCached(string pngPath) => Cache.Get(pngPath);

        internal static bool TryMeasurePair(Sprite sprite, SpriteAnimationReport report, out ReferenceBlobs blobs)
        {
            string path = AssetDatabase.GetAssetPath(sprite);
            blobs = MeasureOrReport(path, null, report);
            if (blobs.IsPair) return true;
            if (blobs.BlobCount >= 0) report.Error(null, $"Anchor sprite '{path}' must contain exactly 2 blobs, found {blobs.BlobCount}.");
            return false;
        }

        internal static ReferenceBlobs MeasureOrReport(string path, string animation, SpriteAnimationReport report)
        {
            try
            {
                return MeasureCached(path);
            }
            catch (Exception exception)
            {
                report.Error(animation, $"Could not read '{path}': {exception.Message}");
                return ReferenceBlobs.Unreadable;
            }
        }

        internal static ReferencePose Solve(ReferenceBlobs frame, ReferenceBlobs neutral, Vector2 bodyPivot, float pixelsPerUnit, bool flipX, bool snapToPixel, float rotationDeadZone)
        {
            if (!frame.IsPair) return new ReferencePose(false, Vector2.zero, 0f);

            Vector2 displacement = frame.Midpoint - neutral.Midpoint;
            if (snapToPixel) displacement = new Vector2(Mathf.Round(displacement.x), Mathf.Round(displacement.y));

            Vector2 offset = neutral.Midpoint - Vector2.Scale(bodyPivot, (Vector2)frame.CanvasSize) + displacement;

            float angle = Mathf.DeltaAngle(neutral.Angle, frame.Angle);
            if (Mathf.Abs(angle) < rotationDeadZone) angle = 0f;

            float sign = flipX ? -1f : 1f;
            return new ReferencePose(true, new Vector2(sign * offset.x / pixelsPerUnit, offset.y / pixelsPerUnit), sign * angle);
        }

        internal static Vector2 NormalizedPivot(ReferenceBlobs neutral)
        {
            return new Vector2(neutral.Midpoint.x / neutral.CanvasSize.x, neutral.Midpoint.y / neutral.CanvasSize.y);
        }

        private static ReferenceBlobs Measure(string pngPath)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(pngPath)))
                {
                    throw new InvalidDataException($"Could not decode '{pngPath}'.");
                }
                return Measure(texture.GetPixels32(), texture.width, texture.height);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static ReferenceBlobs Measure(Color32[] pixels, int width, int height)
        {
            var visited = new bool[pixels.Length];
            var stack = new Stack<int>();
            var centroids = new List<Vector2>();

            for (int start = 0; start < pixels.Length; start++)
            {
                if (visited[start] || pixels[start].a < AlphaThreshold) continue;

                visited[start] = true;
                stack.Push(start);
                long sumX = 0;
                long sumY = 0;
                int count = 0;

                while (stack.Count > 0)
                {
                    int index = stack.Pop();
                    int x = index % width;
                    int y = index / width;
                    sumX += x;
                    sumY += y;
                    count++;

                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;

                            int neighbour = ny * width + nx;
                            if (visited[neighbour] || pixels[neighbour].a < AlphaThreshold) continue;

                            visited[neighbour] = true;
                            stack.Push(neighbour);
                        }
                    }
                }

                if (count < MinBlobPixels) continue;
                centroids.Add(new Vector2(sumX / (float)count + 0.5f, sumY / (float)count + 0.5f));
            }

            var canvas = new Vector2Int(width, height);
            if (centroids.Count != 2) return new ReferenceBlobs(centroids.Count, Vector2.zero, Vector2.zero, canvas);

            Vector2 a = centroids[0];
            Vector2 b = centroids[1];
            return a.x <= b.x ? new ReferenceBlobs(2, a, b, canvas) : new ReferenceBlobs(2, b, a, canvas);
        }
    }
}
