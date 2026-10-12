using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MM.SpriteAnimation
{
    internal static class SpriteAnimationScanner
    {
        internal static SpriteAnimationScan Scan(SpriteAnimationProfile profile)
        {
            var scan = new SpriteAnimationScan();
            if (profile == null)
            {
                scan.AddError("No profile.");
                return scan;
            }

            CheckProfile(profile, scan);
            if (!scan.IsUsable) return scan;

            ControllerBinding binding = ControllerBinding.Read(profile, scan.AddWarning);
            scan.SetController(binding);
            Dictionary<string, string> folders = FindAnimationFolders(profile);
            ulong signature = AnimationFingerprint.ProfileSignature(profile);

            foreach (string name in binding.StateNames.Concat(folders.Keys).Distinct(StringComparer.Ordinal).ToList())
            {
                folders.TryGetValue(name, out string folder);
                var source = new AnimationSource(name, folder, binding.HasState(name));
                if (source.HasFolder) CollectSource(profile, source);
                binding.CheckRow(source);

                bool usable = source.HasFolder && source.Problems.Count == 0;
                string suggestion = source.HasFolder && !source.HasState && binding.RequiresState ? Suggest(name, binding.StateNames) : null;
                string fingerprint = usable ? AnimationFingerprint.Fingerprint(profile, source, signature) : null;
                string timingProblem = null;
                ClipTiming timing = usable ? EffectiveTiming(profile, source, out _, out timingProblem) : null;
                AnimationStatus status = ResolveStatus(source.HasFolder, source.HasState, binding.RequiresState, binding.CanCreateStates, source.Problems.Count > 0, timingProblem != null, profile.FindEntry(name)?.Fingerprint, fingerprint, File.Exists(profile.ClipPath(name)));
                source.Resolve(suggestion, fingerprint, status, timingProblem, timing, status == AnimationStatus.New && binding.RequiresState && !source.HasState);
                scan.Add(source);
            }
            return scan;
        }

        internal static AnimationStatus ResolveStatus(bool hasFolder, bool hasState, bool requiresState, bool canCreateState, bool invalid, bool timingProblem, string builtFingerprint, string fingerprint, bool clipExists)
        {
            if (!hasFolder) return AnimationStatus.NoArt;
            bool missingState = requiresState && !hasState;
            if (missingState && !canCreateState) return AnimationStatus.NoState;
            if (invalid) return AnimationStatus.Invalid;
            if (timingProblem) return AnimationStatus.TimingReset;
            if (missingState || string.IsNullOrEmpty(builtFingerprint)) return AnimationStatus.New;
            if (builtFingerprint != fingerprint || !clipExists) return AnimationStatus.Changed;
            return AnimationStatus.UpToDate;
        }

        internal static ClipTiming EffectiveTiming(SpriteAnimationProfile profile, AnimationSource source, out ClipTiming clipTiming, out string problem)
        {
            clipTiming = null;
            problem = null;
            if (profile == null || source == null || source.Parts.Count == 0 || source.FrameCount == 0) return null;

            int frameRate = Mathf.Max(1, profile.FrameRateOf(source.Name));
            ClipTiming uniform = ClipTiming.Uniform(frameRate, source.FrameCount, profile.MinTickRate);
            if (!profile.IsBuilt(source.Name)) return uniform;

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(profile.ClipPath(source.Name));
            ClipTimingRead read = ClipTiming.ReadBuilt(profile, source.Name, clip, source.Parts[0].Part.RendererPath);
            if (read == null) return uniform;
            if (read.Problem != null)
            {
                problem = read.Problem;
                return uniform;
            }
            if (!read.Matches(source.Parts[0].Frames))
            {
                if (!read.Timing.IsUniform) problem = "Frames were added, removed or reordered since the timing was set.";
                return uniform;
            }
            clipTiming = read.Timing;
            return read.Timing.Retarget(frameRate, profile.MinTickRate);
        }

        private static void CheckProfile(SpriteAnimationProfile profile, SpriteAnimationScan scan)
        {
            if (profile.SpriteRootPath == null) scan.AddError("Sprite Root is not assigned to a folder.");
            if (profile.ClipRootPath == null) scan.AddError("Clip Root is not assigned to a folder.");
            if (profile.PixelsPerUnit <= 0f) scan.AddError("Pixels Per Unit must be positive.");
            if (profile.DefaultFrameRate <= 0) scan.AddError("Default Frame Rate must be positive.");
            PartLayout.Check(profile, scan);
            SpritePivot.Check(profile, scan);
            ReferenceTracking.Check(profile, scan);
            ControllerBinding.Check(profile, scan);
        }

        private static Dictionary<string, string> FindAnimationFolders(SpriteAnimationProfile profile)
        {
            var folders = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string folder in AssetDatabase.GetSubFolders(profile.SpriteRootPath))
            {
                if (PartLayout.IsAnimationFolder(profile, folder)) folders[Path.GetFileName(folder)] = folder;
            }
            return folders;
        }

        private static void CollectSource(SpriteAnimationProfile profile, AnimationSource source)
        {
            foreach (SpritePart part in PartLayout.ActiveParts(profile))
            {
                string directory = PartLayout.Directory(source.Folder, part);
                string stem = PartLayout.Stem(profile, source.Name, part);
                IReadOnlyList<string> frames = part.Optional
                    ? SpriteFrameCollector.CollectOptional(PngFiles.In(directory), stem, part.Label, source.FrameCount, source.Parts[0].Part.Label, source.ProblemSink, source.WarningSink)
                    : CollectRequired(directory, stem, part.Label, source);
                source.AddPart(new PartFrames(part, frames));
            }

            PartFrames first = source.Parts[0];
            foreach (PartFrames other in source.Parts.Skip(1))
            {
                if (first.Frames.Count > 0 && other.Frames.Count > 0 && other.Frames.Count != first.Frames.Count)
                {
                    source.AddProblem($"{other.Part.Label} has {other.Frames.Count} frame(s), {first.Part.Label} has {first.Frames.Count}.");
                }
            }

            if (profile.FrameRateOf(source.Name) <= 0) source.AddProblem("Frame rate must be positive.");
            ReferenceTracking.Collect(profile, source);
        }

        private static IReadOnlyList<string> CollectRequired(string directory, string stem, string label, AnimationSource source)
        {
            if (!Directory.Exists(directory))
            {
                source.AddProblem($"{label}: folder '{directory}' does not exist.");
                return Array.Empty<string>();
            }
            return SpriteFrameCollector.Collect(directory, PngFiles.In(directory), stem, label, source.ProblemSink, source.WarningSink);
        }

        private static string Suggest(string name, IEnumerable<string> candidates)
        {
            string lowered = name.ToLowerInvariant();
            string best = null;
            int bestDistance = Math.Max(2, name.Length / 3) + 1;
            foreach (string candidate in candidates)
            {
                int distance = Distance(lowered, candidate.ToLowerInvariant());
                if (distance >= bestDistance) continue;
                best = candidate;
                bestDistance = distance;
            }
            return best;
        }

        private static int Distance(string a, string b)
        {
            var previous = new int[b.Length + 1];
            var current = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) previous[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                }
                (previous, current) = (current, previous);
            }
            return previous[b.Length];
        }
    }
}
