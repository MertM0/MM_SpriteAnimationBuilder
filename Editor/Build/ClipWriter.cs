using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MM.SpriteAnimation
{
    internal static class ClipWriter
    {
        private const float StopTimeTolerance = 0.00001f;
        private const float EventTolerance = 0.0001f;
        private const float AlignTolerance = 0.0001f;

        internal static bool LoadSprites(AnimationPlan plan, SpriteAnimationReport report)
        {
            plan.Sprites = new List<Sprite[]>(plan.Source.Parts.Count);
            foreach (PartFrames part in plan.Source.Parts)
            {
                var sprites = new Sprite[part.Frames.Count];
                for (int i = 0; i < sprites.Length; i++)
                {
                    if (part.Frames[i] == null) continue;
                    sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(part.Frames[i]);
                    if (sprites[i] == null) report.Error(plan.Source.Name, $"'{part.Frames[i]}' did not import as a Sprite.");
                }
                plan.Sprites.Add(sprites);
            }
            return !report.Failed(plan.Source.Name);
        }

        internal static AnimationClip Write(SpriteAnimationProfile profile, AnimationPlan plan, SpriteAnimationReport report, out ClipTiming timing)
        {
            ReferenceTracking.EnsureFolder(profile, plan.Source);
            AnimationClip clip = LoadOrCreateClip(profile.ClipPath(plan.Source.Name));
            string name = plan.Source.Name;
            string bodyPath = plan.Source.Parts[0].Part.RendererPath;

            var spritePaths = new HashSet<string>(PartLayout.ActiveParts(profile).Select(part => part.RendererPath), StringComparer.Ordinal);
            bool hasPoses = plan.Poses != null;
            Func<EditorCurveBinding, bool> owns = binding =>
                (binding.type == typeof(SpriteRenderer) && binding.propertyName == ClipTiming.SpriteProperty && spritePaths.Contains(binding.path))
                || ReferenceTracking.Owns(profile, hasPoses, binding);

            ClipTiming known = profile.KnownTimingOf(name);
            ClipTimingRead read = ClipTiming.ReadBuilt(profile, name, clip, bodyPath);
            ClipTiming old = read?.Timing;
            bool missedSync = false;
            if (read != null)
            {
                foreach (string warning in read.Warnings) report.Warning(name, warning);
                missedSync = known != null && read.Problem == null && known.FrameCount == old.FrameCount && !known.SameAs(old);
                WarnMisaligned(clip, bodyPath, owns, name, report);
            }

            timing = plan.ResetTiming ? ClipTiming.Uniform(plan.FrameRate, plan.Source.FrameCount, profile.MinTickRate) : plan.Source.Timing;
            int[] frameMap = read?.FrameMapTo(plan.Source.Parts[0].Frames);
            bool retimed = old != null && (!old.SameAs(timing) || !ClipTimingRead.IsIdentity(frameMap, timing.FrameCount));
            if (missedSync)
            {
                report.Warning(name, retimed
                    ? "Timing changed while the builder was not listening; events were moved from the clip's current timing, check them."
                    : "Timing changed while the builder was not listening; events were not re-anchored, check them.");
            }

            ClearBindings(clip, owns);
            for (int i = 0; i < plan.Sprites.Count; i++) SetSpriteCurve(clip, plan.Source.Parts[i].Part.RendererPath, plan.Sprites[i], timing);
            if (hasPoses) ReferenceTracking.WriteCurves(clip, profile, plan.Poses, timing);
            clip.frameRate = timing.TickRate;

            if (retimed)
            {
                AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
                if (events.Length > 0)
                {
                    ClipTiming.RemapEvents(events, old, timing, message => report.Warning(name, message), frameMap);
                    AnimationUtility.SetAnimationEvents(clip, events);
                }
                WarnUnowned(clip, owns, name, report);
            }
            ApplyTiming(clip, timing, plan.Loop, name, report);
            return clip;
        }

        private static void WarnMisaligned(AnimationClip clip, string bodyPath, Func<EditorCurveBinding, bool> owns, string name, SpriteAnimationReport report)
        {
            float[] body = AnimationUtility.GetObjectReferenceCurve(clip, ClipTiming.SpriteBinding(bodyPath)).Select(key => key.time).ToArray();
            var warned = new HashSet<string>(StringComparer.Ordinal) { bodyPath };
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (!owns(binding) || warned.Contains(binding.path)) continue;
                float[] times = AnimationUtility.GetObjectReferenceCurve(clip, binding).Select(key => key.time).ToArray();
                if (!SameTimes(body, times) && warned.Add(binding.path)) report.Warning(name, $"{binding.path} keys did not match {bodyPath}; realigned to {bodyPath}.");
            }
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (!owns(binding) || warned.Contains(binding.path)) continue;
                float[] times = AnimationUtility.GetEditorCurve(clip, binding).keys.Select(key => key.time).ToArray();
                if (!SameTimes(body, times) && warned.Add(binding.path)) report.Warning(name, $"{binding.path} keys did not match {bodyPath}; realigned to {bodyPath}.");
            }
        }

        private static bool SameTimes(float[] a, float[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (Mathf.Abs(a[i] - b[i]) > AlignTolerance) return false;
            }
            return true;
        }

        private static void WarnUnowned(AnimationClip clip, Func<EditorCurveBinding, bool> owns, string name, SpriteAnimationReport report)
        {
            IEnumerable<EditorCurveBinding> bindings = AnimationUtility.GetCurveBindings(clip).Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip));
            foreach (EditorCurveBinding binding in bindings.Where(binding => !owns(binding)))
            {
                report.Warning(name, $"Curve '{binding.path}/{binding.propertyName}' is not owned by the builder and was not retimed.");
            }
        }

        private static AnimationClip LoadOrCreateClip(string path)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null) return clip;

            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static void ClearBindings(AnimationClip clip, Func<EditorCurveBinding, bool> owns)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (owns(binding)) AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
            }
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (owns(binding)) AnimationUtility.SetEditorCurve(clip, binding, null);
            }
        }

        private static void SetSpriteCurve(AnimationClip clip, string path, Sprite[] sprites, ClipTiming timing)
        {
            var keys = new ObjectReferenceKeyframe[sprites.Length + (timing.HasClosingKey ? 1 : 0)];
            for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = timing.StartTime(i), value = sprites[i] };
            if (timing.HasClosingKey) keys[sprites.Length] = new ObjectReferenceKeyframe { time = timing.ClosingTime, value = sprites[sprites.Length - 1] };
            AnimationUtility.SetObjectReferenceCurve(clip, ClipTiming.SpriteBinding(path), keys);
        }

        private static void ApplyTiming(AnimationClip clip, ClipTiming timing, bool loop, string name, SpriteAnimationReport report)
        {
            AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            float stopTime = timing.Length;
            clipSettings.loopTime = loop;
            clipSettings.startTime = 0f;
            if (Mathf.Abs(clipSettings.stopTime - stopTime) > StopTimeTolerance) clipSettings.stopTime = stopTime;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

            foreach (AnimationEvent animationEvent in AnimationUtility.GetAnimationEvents(clip))
            {
                if (animationEvent.time > clipSettings.stopTime + EventTolerance)
                {
                    report.Warning(name, $"Event '{animationEvent.functionName}' at {animationEvent.time.ToString("0.###", CultureInfo.InvariantCulture)}s is past the new clip end ({clipSettings.stopTime.ToString("0.###", CultureInfo.InvariantCulture)}s).");
                }
            }
            EditorUtility.SetDirty(clip);
        }
    }
}
