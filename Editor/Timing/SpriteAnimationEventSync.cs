using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace MM.SpriteAnimation
{
    [InitializeOnLoad]
    internal static class SpriteAnimationEventSync
    {
        private const string UndoName = "Re-anchor Animation Events";
        private const float MovedTolerance = 0.00001f;

        private static readonly string[] SearchRoots = { "Assets" };

        private static Dictionary<string, (SpriteAnimationProfile Profile, string Animation)> owners;

        static SpriteAnimationEventSync()
        {
            AnimationUtility.onCurveWasModified += OnCurveWasModified;
            EditorApplication.projectChanged += () => owners = null;
        }

        private static void OnCurveWasModified(AnimationClip clip, EditorCurveBinding binding, AnimationUtility.CurveModifiedType type)
        {
            if (type != AnimationUtility.CurveModifiedType.CurveModified || Undo.isProcessing || SpriteAnimationBuilder.IsWriting) return;
            if (clip == null || !binding.isPPtrCurve || binding.type != typeof(SpriteRenderer) || binding.propertyName != ClipTiming.SpriteProperty) return;
            if (!TryFindOwner(clip, out SpriteAnimationProfile profile, out string animation) || !profile.EventSync) return;

            string bodyPath = PartLayout.ActiveParts(profile)[0].RendererPath;
            if (binding.path != bodyPath) return;

            ClipTiming known = profile.KnownTimingOf(animation);
            if (known == null) return;

            ClipTimingRead read = ClipTiming.Read(clip, bodyPath, known.FrameRate, known.TickRate);
            if (read == null || read.Problem != null) return;
            foreach (string warning in read.Warnings) Debug.LogWarning($"{animation}: {warning}", clip);
            if (read.Timing.FrameCount != known.FrameCount || !read.IsInFileOrder())
            {
                Debug.LogWarning($"{animation}: frames were reordered, added or removed in the Animation window, so events were not moved. Undo, or move every frame after the edited one (including the closing key) together.", clip);
                return;
            }
            if (known.SameAs(read.Timing)) return;

            Undo.RecordObject(clip, UndoName);
            Undo.RecordObject(profile, UndoName);
            profile.SetKnownTiming(animation, read.Timing);
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
            if (events.Length == 0) return;

            var before = new float[events.Length];
            for (int i = 0; i < events.Length; i++) before[i] = events[i].time;
            ClipTiming.RemapEvents(events, known, read.Timing, message => Debug.LogWarning($"{animation}: {message}", clip));
            AnimationUtility.SetAnimationEvents(clip, events);
            ReportMoved(animation, clip, events, before, read.Timing);
        }

        private static void ReportMoved(string animation, AnimationClip clip, AnimationEvent[] events, float[] before, ClipTiming timing)
        {
            var moved = new List<string>();
            for (int i = 0; i < events.Length; i++)
            {
                if (Mathf.Abs(events[i].time - before[i]) <= MovedTolerance) continue;
                int frame = timing.FrameAt(events[i].time, out _) + 1;
                moved.Add($"{events[i].functionName} (frame {frame}) {Seconds(before[i])} -> {Seconds(events[i].time)}");
            }
            if (moved.Count > 0) Debug.Log($"{animation}: events moved with their frames: {string.Join(", ", moved)}", clip);
        }

        private static string Seconds(float time) => time.ToString("0.###", CultureInfo.InvariantCulture) + " s";

        private static bool TryFindOwner(AnimationClip clip, out SpriteAnimationProfile profile, out string animation)
        {
            if (owners == null) owners = FindOwners();
            if (owners.TryGetValue(AssetDatabase.GetAssetPath(clip), out (SpriteAnimationProfile Profile, string Animation) owner) && owner.Profile != null)
            {
                profile = owner.Profile;
                animation = owner.Animation;
                return true;
            }
            profile = null;
            animation = null;
            return false;
        }

        private static Dictionary<string, (SpriteAnimationProfile Profile, string Animation)> FindOwners()
        {
            var result = new Dictionary<string, (SpriteAnimationProfile Profile, string Animation)>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(SpriteAnimationProfile), SearchRoots))
            {
                var profile = AssetDatabase.LoadAssetAtPath<SpriteAnimationProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (profile == null || profile.ClipRootPath == null) continue;

                foreach (AnimationEntry entry in profile.Animations)
                {
                    if (entry.IsBuilt) result[profile.ClipPath(entry.Name)] = (profile, entry.Name);
                }
            }
            return result;
        }
    }
}
