using System;
using System.Collections.Generic;

namespace MM.SpriteAnimation
{
    internal sealed class SpriteAnimationScan
    {
        private readonly List<AnimationSource> sources = new List<AnimationSource>();
        private readonly Dictionary<string, AnimationSource> byName = new Dictionary<string, AnimationSource>(StringComparer.Ordinal);
        private readonly List<string> profileErrors = new List<string>();
        private readonly List<string> profileWarnings = new List<string>();

        internal IReadOnlyList<AnimationSource> Sources => sources;
        internal IReadOnlyList<string> ProfileErrors => profileErrors;
        internal IReadOnlyList<string> ProfileWarnings => profileWarnings;
        internal bool IsUsable => profileErrors.Count == 0;
        internal ControllerBinding Controller { get; private set; }

        internal void SetController(ControllerBinding binding) => Controller = binding;

        internal AnimationSource Find(string name) => name != null && byName.TryGetValue(name, out AnimationSource source) ? source : null;

        internal void Add(AnimationSource source)
        {
            sources.Add(source);
            byName[source.Name] = source;
        }

        internal void AddError(string message) => profileErrors.Add(message);
        internal void AddWarning(string message) => profileWarnings.Add(message);
    }
}
