using System;
using System.Collections.Generic;

namespace MM.SpriteAnimation
{
    public sealed class SpriteAnimationReport
    {
        private readonly List<ReportEntry> entries = new List<ReportEntry>();
        private readonly HashSet<string> failed = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> built = new List<string>();

        public IReadOnlyList<ReportEntry> Entries => entries;
        public IReadOnlyList<string> Built => built;
        public bool HasErrors => entries.Exists(entry => entry.Severity == ReportSeverity.Error);

        public bool Failed(string animation) => animation != null && failed.Contains(animation);

        internal void Error(string animation, string message)
        {
            Add(ReportSeverity.Error, animation, message);
            if (animation != null) failed.Add(animation);
        }

        internal void Warning(string animation, string message) => Add(ReportSeverity.Warning, animation, message);

        internal void Info(string animation, string message) => Add(ReportSeverity.Info, animation, message);

        internal void MarkBuilt(string animation) => built.Add(animation);

        private void Add(ReportSeverity severity, string animation, string message)
        {
            entries.Add(new ReportEntry(DateTime.Now, severity, animation, message));
        }
    }
}
