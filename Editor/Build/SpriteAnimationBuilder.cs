using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MM.SpriteAnimation
{
    public static class SpriteAnimationBuilder
    {
        private const float PlanningShare = 0.4f;
        private const float ImportShare = 0.1f;

        internal static bool IsWriting { get; private set; }

        public static SpriteAnimationReport Validate(SpriteAnimationProfile profile, IEnumerable<string> names = null, Action<string, float> progress = null)
        {
            var report = new SpriteAnimationReport();
            foreach (AnimationPlan plan in AnimationPlanner.CreatePlans(profile, names, report, out _, progress, 1f, false))
            {
                report.Info(plan.Source.Name, "Validation passed.");
            }
            return report;
        }

        public static SpriteAnimationReport Build(SpriteAnimationProfile profile, IEnumerable<string> names = null, Action<string, float> progress = null, bool resetTiming = false)
        {
            var report = new SpriteAnimationReport();
            List<AnimationPlan> plans = AnimationPlanner.CreatePlans(profile, names, report, out ProfileContext context, progress, PlanningShare, resetTiming);
            if (plans.Count == 0) return report;

            progress?.Invoke("Applying sprite import settings...", PlanningShare);
            int reimported = SpriteImport.Apply(profile, plans, context);
            ControllerBinding binding = context.Scan.Controller;

            try
            {
                IsWriting = true;
                for (int i = 0; i < plans.Count; i++)
                {
                    AnimationPlan plan = plans[i];
                    progress?.Invoke($"Writing {plan.Source.Name} ({i + 1}/{plans.Count})", PlanningShare + ImportShare + (1f - PlanningShare - ImportShare) * i / plans.Count);
                    try
                    {
                        WriteRow(profile, plan, binding, report);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        report.Error(plan.Source.Name, $"{exception.GetType().Name}: {exception.Message} (stack trace in the Console)");
                    }
                }
            }
            finally
            {
                IsWriting = false;
                binding.Save();
                AssetDatabase.SaveAssetIfDirty(profile);
            }

            report.Info(null, $"Built {report.Built.Count} of {plans.Count} planned animation(s); reimported {reimported} sprite(s).");
            return report;
        }

        private static void WriteRow(SpriteAnimationProfile profile, AnimationPlan plan, ControllerBinding binding, SpriteAnimationReport report)
        {
            string name = plan.Source.Name;
            if (!ClipWriter.LoadSprites(plan, report)) return;

            AnimationClip clip = ClipWriter.Write(profile, plan, report, out ClipTiming timing);
            binding.Assign(name, clip);
            AssetDatabase.SaveAssetIfDirty(clip);
            profile.SetFingerprint(name, plan.Source.Fingerprint);
            profile.SetKnownTiming(name, timing);
            report.MarkBuilt(name);
            report.Info(name, $"Built {plan.Source.FrameCount} frame(s) at {plan.FrameRate} fps ({timing.TickRate} ticks/s){(timing.IsUniform ? string.Empty : ", custom timing kept")}{(plan.Loop ? ", looping" : string.Empty)}.");
        }
    }
}
