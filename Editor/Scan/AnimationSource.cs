using System;
using System.Collections.Generic;

namespace MM.SpriteAnimation
{
    internal sealed class AnimationSource
    {
        private readonly List<PartFrames> parts = new List<PartFrames>();
        private readonly List<string> problems = new List<string>();
        private readonly List<string> warnings = new List<string>();

        internal AnimationSource(string name, string folder, bool hasState, string artName)
        {
            Name = name;
            Folder = folder;
            HasState = hasState;
            ArtName = artName;
        }

        internal string Name { get; }
        internal string Folder { get; }
        internal bool HasFolder => Folder != null;
        internal bool HasState { get; }
        internal string ArtName { get; }
        internal bool IsFallback => !string.Equals(ArtName, Name, StringComparison.Ordinal);
        internal IReadOnlyList<PartFrames> Parts => parts;
        internal IReadOnlyList<string> ReferenceFrames { get; private set; }
        internal IReadOnlyList<string> Problems => problems;
        internal IReadOnlyList<string> Warnings => warnings;
        internal ICollection<string> ProblemSink => problems;
        internal ICollection<string> WarningSink => warnings;
        internal int FrameCount => parts.Count > 0 ? parts[0].Frames.Count : 0;
        internal string SuggestedState { get; private set; }
        internal string Fingerprint { get; private set; }
        internal string TimingProblem { get; private set; }
        internal ClipTiming Timing { get; private set; }
        internal AnimationStatus Status { get; private set; }
        internal bool CreatesState { get; private set; }
        internal string BaseClip { get; private set; }
        internal bool PlaysBaseClip => BaseClip != null;
        internal bool IsBuildable => Status == AnimationStatus.New || Status == AnimationStatus.Changed || Status == AnimationStatus.UpToDate;
        internal bool NeedsBuild => Status == AnimationStatus.New || Status == AnimationStatus.Changed;
        internal bool InBuildAll => HasFolder && Status != AnimationStatus.NoState;

        internal void AddPart(PartFrames part) => parts.Add(part);
        internal void SetReferenceFrames(IReadOnlyList<string> frames) => ReferenceFrames = frames;
        internal void AddProblem(string message) => problems.Add(message);
        internal void AddWarning(string message) => warnings.Add(message);
        internal void SetBaseClip(string clip) => BaseClip = clip;

        internal void Resolve(string suggestedState, string fingerprint, AnimationStatus status, string timingProblem, ClipTiming timing, bool createsState)
        {
            SuggestedState = suggestedState;
            Fingerprint = fingerprint;
            Status = status;
            TimingProblem = timingProblem;
            Timing = timing;
            CreatesState = createsState;
        }
    }
}
