using System;

namespace MM.SpriteAnimation
{
    public readonly struct ReportEntry
    {
        public readonly DateTime Time;
        public readonly ReportSeverity Severity;
        public readonly string Animation;
        public readonly string Message;

        internal ReportEntry(DateTime time, ReportSeverity severity, string animation, string message)
        {
            Time = time;
            Severity = severity;
            Animation = animation;
            Message = message;
        }

        public override string ToString() => Animation == null ? Message : $"{Animation}: {Message}";
    }
}
