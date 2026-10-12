using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MM.SpriteAnimation
{
    internal static class AnimationPlanner
    {
        internal static List<AnimationPlan> CreatePlans(SpriteAnimationProfile profile, IEnumerable<string> names, SpriteAnimationReport report, out ProfileContext context, Action<string, float> progress, float share, bool resetTiming)
        {
            context = new ProfileContext { Scan = SpriteAnimationScanner.Scan(profile) };
            var plans = new List<AnimationPlan>();
            foreach (string warning in context.Scan.ProfileWarnings) report.Warning(null, warning);
            foreach (string error in context.Scan.ProfileErrors) report.Error(null, error);
            if (!context.Scan.IsUsable) return plans;

            if (!SpritePivot.MeasureAnchor(profile, report, out ReferenceBlobs? anchor)) return plans;
            if (!ReferenceTracking.MeasureAnchor(profile, report, out ReferenceBlobs? tracked)) return plans;
            context.Anchor = anchor;
            context.Tracked = tracked;
            context.BodyPivot = SpritePivot.Normalized(profile, anchor);

            IEnumerable<string> requested = names ?? context.Scan.Sources.Where(source => source.InBuildAll).Select(source => source.Name);
            List<string> unique = requested.Distinct(StringComparer.Ordinal).ToList();
            for (int i = 0; i < unique.Count; i++)
            {
                progress?.Invoke($"Checking {unique[i]} ({i + 1}/{unique.Count})", share * i / unique.Count);
                AnimationPlan plan = PlanAnimation(profile, context, unique[i], report, resetTiming);
                if (plan != null) plans.Add(plan);
            }
            return plans;
        }

        private static AnimationPlan PlanAnimation(SpriteAnimationProfile profile, ProfileContext context, string name, SpriteAnimationReport report, bool resetTiming)
        {
            AnimationSource source = context.Scan.Find(name);
            if (!CheckSource(source, name, report, resetTiming)) return null;
            foreach (string warning in source.Warnings) report.Warning(name, warning);

            Vector2Int? canvas = context.Anchor?.CanvasSize ?? context.Tracked?.CanvasSize;
            foreach (PartFrames part in source.Parts)
            {
                foreach (string path in part.Frames)
                {
                    if (path != null) CheckImportedCanvas(path, name, ref canvas, report);
                }
            }

            List<ReferencePose> poses = ReferenceTracking.SolvePoses(profile, source, context.Tracked, context.BodyPivot, report);

            if (report.Failed(name)) return null;
            return new AnimationPlan
            {
                Source = source,
                FrameRate = profile.FrameRateOf(name),
                Loop = profile.LoopOf(name),
                Poses = poses,
                ResetTiming = resetTiming,
            };
        }

        private static bool CheckSource(AnimationSource source, string name, SpriteAnimationReport report, bool resetTiming)
        {
            if (source == null)
            {
                report.Error(name, "Not found in this profile.");
                return false;
            }

            switch (source.Status)
            {
                case AnimationStatus.NoArt:
                    report.Error(name, "No sprite folder.");
                    return false;
                case AnimationStatus.NoState:
                    report.Error(name, source.SuggestedState != null
                        ? $"The controller has no state '{name}'. Did you mean '{source.SuggestedState}'?"
                        : $"The controller has no state '{name}'.");
                    return false;
                case AnimationStatus.Invalid:
                    foreach (string problem in source.Problems) report.Error(name, problem);
                    return false;
                case AnimationStatus.TimingReset:
                    if (resetTiming) return true;
                    report.Error(name, $"{source.TimingProblem} Use ... > Reset Timing and Build.");
                    return false;
                default:
                    return true;
            }
        }

        private static void CheckImportedCanvas(string path, string name, ref Vector2Int? canvas, SpriteAnimationReport report)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            {
                report.Error(name, $"'{path}' is not imported yet. Focus Unity or use Assets > Refresh.");
                return;
            }

            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            var size = new Vector2Int(width, height);
            if (!canvas.HasValue) canvas = size;
            else if (size != canvas.Value) report.Error(name, $"'{path}' is {width}x{height}; every sprite must share the {canvas.Value.x}x{canvas.Value.y} canvas.");
        }
    }
}
