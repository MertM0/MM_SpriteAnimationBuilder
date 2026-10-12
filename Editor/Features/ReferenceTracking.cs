using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MM.SpriteAnimation
{
    internal static class ReferenceTracking
    {
        private static readonly string[] PositionProperties = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };
        private static readonly string[] RotationProperties = { "localEulerAnglesRaw.x", "localEulerAnglesRaw.y", "localEulerAnglesRaw.z" };
        private static readonly string[] ScaleProperties = { "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" };

        internal static void Check(SpriteAnimationProfile profile, SpriteAnimationScan scan)
        {
            TrackingSettings tracking = profile.Tracking;
            if (!tracking.Enabled) return;

            if (tracking.Token.Length == 0) scan.AddError("Tracking token is empty.");
            if (tracking.TargetPath.Length == 0) scan.AddError("Tracking target path is empty.");
            if (tracking.TrackedProfile == null)
            {
                scan.AddError("Tracked Profile is not assigned.");
                return;
            }
            if (tracking.TrackedProfile.AnchorSprite == null) scan.AddError($"Tracked profile '{tracking.TrackedProfile.DisplayName}' has no Anchor Sprite.");
            else if (tracking.TrackedProfile.Pivot != SpritePivotMode.Anchor) scan.AddWarning($"Tracked profile '{tracking.TrackedProfile.DisplayName}' does not use the Anchor pivot; its sprites will not turn around the anchor.");
        }

        internal static void Collect(SpriteAnimationProfile profile, AnimationSource source)
        {
            TrackingSettings tracking = profile.Tracking;
            if (!tracking.Enabled) return;

            string directory = $"{source.Folder}/{tracking.FolderName}";
            string[] pngs = PngFiles.In(directory).ToArray();
            if (pngs.Length == 0)
            {
                source.SetReferenceFrames(Array.Empty<string>());
                return;
            }

            IReadOnlyList<string> frames = SpriteFrameCollector.Collect(directory, pngs, $"{source.Name}_{tracking.Token}_", tracking.FolderName, source.ProblemSink, source.WarningSink);
            if (frames.Count > 0 && source.FrameCount > 0 && frames.Count != source.FrameCount)
            {
                source.AddProblem($"{tracking.FolderName} has {frames.Count} frame(s), {source.Parts[0].Part.Label} has {source.FrameCount}.");
            }
            source.SetReferenceFrames(frames);
        }

        internal static bool MeasureAnchor(SpriteAnimationProfile profile, SpriteAnimationReport report, out ReferenceBlobs? tracked)
        {
            tracked = null;
            if (!profile.Tracking.Enabled) return true;
            if (!ReferencePoseAnalyzer.TryMeasurePair(profile.Tracking.TrackedProfile.AnchorSprite, report, out ReferenceBlobs blobs)) return false;

            tracked = blobs;
            return true;
        }

        internal static List<ReferencePose> SolvePoses(SpriteAnimationProfile profile, AnimationSource source, ReferenceBlobs? tracked, Vector2 bodyPivot, SpriteAnimationReport report)
        {
            if (!tracked.HasValue || source.ReferenceFrames == null) return null;

            ReferenceBlobs neutral = tracked.Value;
            TrackingSettings tracking = profile.Tracking;
            if (source.ReferenceFrames.Count == 0) return Enumerable.Repeat(new ReferencePose(false, Vector2.zero, 0f), source.FrameCount).ToList();

            var poses = new List<ReferencePose>(source.ReferenceFrames.Count);
            for (int i = 0; i < source.ReferenceFrames.Count; i++)
            {
                ReferenceBlobs blobs = ReferencePoseAnalyzer.MeasureOrReport(source.ReferenceFrames[i], source.Name, report);
                if (blobs.BlobCount < 0) continue;

                if (blobs.CanvasSize != neutral.CanvasSize)
                {
                    report.Error(source.Name, $"{tracking.FolderName} frame {i + 1:00} is {blobs.CanvasSize.x}x{blobs.CanvasSize.y}, the anchor is {neutral.CanvasSize.x}x{neutral.CanvasSize.y}.");
                }
                if (!blobs.IsEmpty && !blobs.IsPair)
                {
                    report.Error(source.Name, $"{tracking.FolderName} frame {i + 1:00}: {blobs.BlobCount} blob(s) found (expected 0 or 2; shapes touching or stray strokes).");
                }
                poses.Add(ReferencePoseAnalyzer.Solve(blobs, neutral, bodyPivot, profile.PixelsPerUnit, profile.FlipX, tracking.SnapToPixel, tracking.RotationDeadZone));
            }
            return poses;
        }

        internal static void EnsureFolder(SpriteAnimationProfile profile, AnimationSource source)
        {
            if (!profile.Tracking.Enabled || source.Folder == null) return;
            Directory.CreateDirectory($"{source.Folder}/{profile.Tracking.FolderName}");
        }

        internal static bool Owns(SpriteAnimationProfile profile, bool hasPoses, EditorCurveBinding binding)
        {
            return hasPoses && binding.type == typeof(Transform) && binding.path == profile.Tracking.TargetPath;
        }

        internal static void WriteCurves(AnimationClip clip, SpriteAnimationProfile profile, IReadOnlyList<ReferencePose> poses, ClipTiming timing)
        {
            string path = profile.Tracking.TargetPath;
            int count = poses.Count;
            var x = new float[count];
            var y = new float[count];
            var angle = new float[count];
            var scale = new float[count];
            var zeros = new float[count];
            float[] ones = Enumerable.Repeat(1f, count).ToArray();

            ReferencePose held = poses.FirstOrDefault(pose => pose.Visible);
            for (int i = 0; i < count; i++)
            {
                if (poses[i].Visible) held = poses[i];
                x[i] = held.Position.x;
                y[i] = held.Position.y;
                angle[i] = held.Angle;
                scale[i] = poses[i].Visible ? 1f : 0f;
            }

            SetConstantCurves(clip, path, PositionProperties, new[] { x, y, zeros }, timing);
            SetConstantCurves(clip, path, RotationProperties, new[] { zeros, zeros, angle }, timing);
            SetConstantCurves(clip, path, ScaleProperties, new[] { scale, scale, ones }, timing);
        }

        private static void SetConstantCurves(AnimationClip clip, string path, string[] properties, float[][] values, ClipTiming timing)
        {
            for (int axis = 0; axis < properties.Length; axis++)
            {
                var curve = new AnimationCurve();
                for (int i = 0; i < values[axis].Length; i++) curve.AddKey(new Keyframe(timing.StartTime(i), values[axis][i]));
                if (timing.HasClosingKey) curve.AddKey(new Keyframe(timing.ClosingTime, values[axis][values[axis].Length - 1]));
                for (int i = 0; i < curve.length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), properties[axis]), curve);
            }
        }

        internal static ReferencePose[] PreviewPoses(SpriteAnimationProfile profile, AnimationSource source, Vector2 bodyPivot, out Sprite anchor, out string problem)
        {
            anchor = null;
            problem = null;
            TrackingSettings tracking = profile.Tracking;
            if (!tracking.Enabled || source.ReferenceFrames == null || tracking.TrackedProfile == null || tracking.TrackedProfile.AnchorSprite == null) return null;

            try
            {
                ReferenceBlobs neutral = ReferencePoseAnalyzer.MeasureCached(AssetDatabase.GetAssetPath(tracking.TrackedProfile.AnchorSprite));
                if (!neutral.IsPair)
                {
                    problem = "The tracked anchor sprite does not contain exactly 2 blobs.";
                    return null;
                }
                ReferencePose[] poses = source.ReferenceFrames
                    .Select(path => ReferencePoseAnalyzer.Solve(ReferencePoseAnalyzer.MeasureCached(path), neutral, bodyPivot, profile.PixelsPerUnit, false, tracking.SnapToPixel, tracking.RotationDeadZone))
                    .ToArray();
                anchor = tracking.TrackedProfile.AnchorSprite;
                return poses;
            }
            catch (Exception exception)
            {
                problem = $"Could not read the reference frames: {exception.Message}";
                return null;
            }
        }
    }
}
