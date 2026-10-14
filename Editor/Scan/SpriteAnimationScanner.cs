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
            Dictionary<string, string> folders = FindAnimationFolders(profile, scan);
            ulong signature = AnimationFingerprint.ProfileSignature(profile);

            IEnumerable<string> fallbackRows = profile.Fallbacks.Where(rule => folders.ContainsKey(rule.Source)).Select(rule => rule.Animation);
            foreach (string name in binding.StateNames.Concat(folders.Keys).Concat(fallbackRows).Distinct(StringComparer.Ordinal).ToList())
            {
                string artName = name;
                string fallback = null;
                if (!folders.TryGetValue(name, out string folder))
                {
                    fallback = AnimationFallbacks.SourceOf(profile, name);
                    if (fallback != null && folders.TryGetValue(fallback, out folder)) artName = fallback;
                }
                var source = new AnimationSource(name, folder, binding.HasState(name), artName);
                if (folder == null && fallback != null) source.AddWarning($"No '{name}' folder, and the fallback '{fallback}' has none either.");
                if (source.HasFolder) CollectSource(profile, source);
                binding.CheckRow(source);
                if (!source.HasFolder)
                {
                    AnimationClip played = binding.BaseClipPlayed(name);
                    if (played != null)
                    {
                        source.SetBaseClip(played.name);
                        source.AddWarning($"No art; in game this state plays '{played.name}' from the base controller.");
                    }
                }

                bool usable = source.HasFolder && source.Problems.Count == 0;
                string suggestion = source.HasFolder && !source.HasState && binding.RequiresState ? Suggest(name, binding.StateNames) : null;
                string fingerprint = usable ? AnimationFingerprint.Fingerprint(profile, source, signature) : null;
                string clipPath = profile.ClipPath(name);
                bool clipExists = File.Exists(clipPath);
                bool assigned = !clipExists || binding.IsAssigned(name, clipPath);
                if (usable && profile.IsBuilt(name) && !assigned) source.AddWarning($"State '{name}' does not play this clip; build to reassign.");
                string timingProblem = null;
                ClipTiming timing = usable ? EffectiveTiming(profile, source, out _, out timingProblem) : null;
                AnimationStatus status = ResolveStatus(source.HasFolder, source.HasState, binding.RequiresState, binding.CanCreateStates, source.Problems.Count > 0, timingProblem != null, profile.FindEntry(name)?.Fingerprint, fingerprint, clipExists && assigned);
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
            ProfileVariants.Check(profile, scan);
            AnimationFallbacks.Check(profile, scan);
        }

        private static Dictionary<string, string> FindAnimationFolders(SpriteAnimationProfile profile, SpriteAnimationScan scan)
        {
            var folders = new Dictionary<string, string>(StringComparer.Ordinal);
            string prefix = profile.FolderPrefix;
            int skipped = 0;
            foreach (string folder in AssetDatabase.GetSubFolders(profile.SpriteRootPath))
            {
                if (!PartLayout.IsAnimationFolder(profile, folder)) continue;
                string name = Path.GetFileName(folder);
                if (prefix.Length > 0)
                {
                    if (!name.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        skipped++;
                        continue;
                    }
                    name = name.Substring(prefix.Length);
                    if (name.Length == 0) continue;
                }
                folders[name] = folder;
            }
            if (skipped > 0) scan.AddWarning($"{skipped} folder(s) under Sprite Root do not start with '{prefix}' and were skipped.");
            return folders;
        }

        private static void CollectSource(SpriteAnimationProfile profile, AnimationSource source)
        {
            foreach (SpritePart part in PartLayout.ActiveParts(profile))
            {
                string directory = PartLayout.Directory(source.Folder, part);
                string stem = PartLayout.Stem(profile, source.ArtName, part);
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
