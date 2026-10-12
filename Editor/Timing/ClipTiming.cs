using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MM.SpriteAnimation
{
    internal sealed class ClipTiming
    {
        #region Constants and state

        internal const int DefaultMinTickRate = 60;
        internal const string SpriteProperty = "m_Sprite";
        private const float GridTolerance = 0.01f;
        private const float FrameTolerance = 0.00001f;
        private const float CompareTolerance = 0.00001f;

        private readonly int[] ticks;
        private readonly int[] starts;

        private ClipTiming(int frameRate, int tickRate, int[] frameTicks)
        {
            FrameRate = frameRate;
            TickRate = tickRate;
            ticks = frameTicks;
            starts = new int[frameTicks.Length];
            int total = 0;
            for (int i = 0; i < frameTicks.Length; i++)
            {
                starts[i] = total;
                total += frameTicks[i];
            }
            TotalTicks = total;
        }

        internal int FrameRate { get; }
        internal int TickRate { get; }
        internal int FrameCount => ticks.Length;
        internal int TotalTicks { get; }
        internal int TicksPerFrame => Mathf.Max(1, TickRate / FrameRate);
        internal float Length => (float)TotalTicks / TickRate;
        internal bool HasClosingKey => ticks.Length > 0 && ticks[ticks.Length - 1] >= 2;
        internal float ClosingTime => (float)(TotalTicks - 1) / TickRate;

        internal bool IsUniform
        {
            get
            {
                foreach (int value in ticks)
                {
                    if (value != TicksPerFrame) return false;
                }
                return true;
            }
        }

        internal string Signature
        {
            get
            {
                var text = new StringBuilder();
                text.Append(FrameRate.ToString(CultureInfo.InvariantCulture)).Append('/').Append(TickRate.ToString(CultureInfo.InvariantCulture)).Append(':');
                for (int i = 0; i < ticks.Length; i++)
                {
                    if (i > 0) text.Append(',');
                    text.Append(ticks[i].ToString(CultureInfo.InvariantCulture));
                }
                return text.ToString();
            }
        }

        #endregion

        #region Factories

        internal static int TickRateOf(int frameRate, int minTickRate) => frameRate * ((minTickRate + frameRate - 1) / frameRate);

        internal static int TicksPerFrameOf(int frameRate, int minTickRate) => TickRateOf(frameRate, minTickRate) / frameRate;

        internal static (int FrameRate, int TickRate) BuiltGridOf(ClipTiming known, AnimationClip clip, int frameRate, int minTickRate)
        {
            if (known != null) return (known.FrameRate, known.TickRate);
            if (clip == null || frameRate <= 0) return (0, 0);

            int tickRate = TickRateOf(frameRate, minTickRate);
            return tickRate != frameRate && Mathf.Abs(clip.frameRate - tickRate) <= GridTolerance ? (frameRate, tickRate) : (0, 0);
        }

        internal static ClipTiming Uniform(int frameRate, int frameCount, int minTickRate)
        {
            var frameTicks = new int[frameCount];
            int per = TicksPerFrameOf(frameRate, minTickRate);
            for (int i = 0; i < frameCount; i++) frameTicks[i] = per;
            return new ClipTiming(frameRate, TickRateOf(frameRate, minTickRate), frameTicks);
        }

        internal static bool TryParse(string signature, out ClipTiming timing)
        {
            timing = null;
            if (string.IsNullOrEmpty(signature)) return false;

            int slash = signature.IndexOf('/');
            int colon = signature.IndexOf(':');
            if (slash <= 0 || colon <= slash + 1 || colon == signature.Length - 1) return false;
            if (!int.TryParse(signature.Substring(0, slash), NumberStyles.None, CultureInfo.InvariantCulture, out int frameRate) || frameRate <= 0) return false;
            if (!int.TryParse(signature.Substring(slash + 1, colon - slash - 1), NumberStyles.None, CultureInfo.InvariantCulture, out int tickRate) || tickRate <= 0) return false;

            string[] parts = signature.Substring(colon + 1).Split(',');
            var frameTicks = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out frameTicks[i]) || frameTicks[i] <= 0) return false;
            }
            timing = new ClipTiming(frameRate, tickRate, frameTicks);
            return true;
        }

        internal static EditorCurveBinding SpriteBinding(string path) => EditorCurveBinding.PPtrCurve(path, typeof(SpriteRenderer), SpriteProperty);

        #endregion

        #region Clip reading and events

        internal static void RemapEvents(AnimationEvent[] events, ClipTiming from, ClipTiming to, Action<string> warn, int[] frameMap = null)
        {
            foreach (AnimationEvent animationEvent in events)
            {
                int frame = from.FrameAt(animationEvent.time, out float fraction);
                int target = frameMap == null ? frame : frame < frameMap.Length ? frameMap[frame] : -1;
                if (target < 0 || target >= to.FrameCount)
                {
                    target = Mathf.Min(frame, to.FrameCount - 1);
                    warn?.Invoke($"Event '{animationEvent.functionName}' was on old frame {frame + 1}, which no longer exists; moved to frame {target + 1}.");
                    fraction = 0f;
                }
                float tick = to.StartTick(target) + fraction * to.Ticks(target);
                animationEvent.time = Mathf.Round(tick) / to.TickRate;
            }
        }

        internal static ClipTimingRead ReadBuilt(SpriteAnimationProfile profile, string animation, AnimationClip clip, string bodyPath)
        {
            if (!profile.IsBuilt(animation)) return null;
            int frameRate = Mathf.Max(1, profile.FrameRateOf(animation));
            (int builtFrameRate, int builtTickRate) = BuiltGridOf(profile.KnownTimingOf(animation), clip, frameRate, profile.MinTickRate);
            return Read(clip, bodyPath, builtFrameRate, builtTickRate);
        }

        internal static ClipTimingRead Read(AnimationClip clip, string bodyPath, int builtFrameRate, int builtTickRate)
        {
            if (clip == null) return null;
            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, SpriteBinding(bodyPath));
            int sampleRate = Mathf.RoundToInt(clip.frameRate);
            if (keys == null || keys.Length == 0 || sampleRate <= 0) return null;

            string problem = null;
            if (builtTickRate > 0 && Mathf.Abs(clip.frameRate - builtTickRate) > GridTolerance)
            {
                problem = $"Samples was changed in the Animation window ({clip.frameRate.ToString("0.##", CultureInfo.InvariantCulture)} instead of {builtTickRate.ToString(CultureInfo.InvariantCulture)}).";
            }

            var warnings = new List<string>();
            var keyTicks = new int[keys.Length];
            bool offGrid = false;
            for (int i = 0; i < keys.Length; i++)
            {
                float exact = keys[i].time * clip.frameRate;
                keyTicks[i] = Mathf.RoundToInt(exact);
                if (Mathf.Abs(exact - keyTicks[i]) > GridTolerance) offGrid = true;
            }
            if (offGrid) warnings.Add("Some Body keys were off the Samples grid and were rounded.");
            if (keyTicks[0] != 0)
            {
                warnings.Add("The first Body key was not at 0; the first frame now starts at 0.");
                keyTicks[0] = 0;
            }

            int last = keys.Length - 1;
            bool closing = last > 0 && keys[last].value != null && keys[last].value == keys[last - 1].value;
            if (builtFrameRate <= 0 && closing)
            {
                problem = "The clip uses the tick grid but the profile has no timing record for it.";
            }
            int count = closing ? keys.Length - 1 : keys.Length;
            int end = keyTicks[last] + 1;

            var frameTicks = new int[count];
            var paths = new string[count];
            for (int i = 0; i < count; i++)
            {
                int next = i + 1 < count ? keyTicks[i + 1] : end;
                frameTicks[i] = Mathf.Max(1, next - keyTicks[i]);
                paths[i] = keys[i].value != null ? AssetDatabase.GetAssetPath(keys[i].value) : null;
            }

            int frameRate = builtFrameRate > 0 ? builtFrameRate : sampleRate;
            return new ClipTimingRead(new ClipTiming(frameRate, sampleRate, frameTicks), paths, problem, warnings);
        }

        #endregion

        #region Frame queries

        internal int Ticks(int frame) => ticks[frame];

        internal int StartTick(int frame) => starts[frame];

        internal float StartTime(int frame) => (float)starts[frame] / TickRate;

        internal float Duration(int frame) => (float)ticks[frame] / TickRate;

        internal int FrameAtTick(int tick)
        {
            int frame = 0;
            for (int i = 1; i < starts.Length; i++)
            {
                if (starts[i] <= tick) frame = i;
            }
            return frame;
        }

        internal int FrameAt(float time, out float fraction)
        {
            int frame = 0;
            for (int i = 1; i < starts.Length; i++)
            {
                if (StartTime(i) <= time + FrameTolerance) frame = i;
            }
            float duration = Duration(frame);
            fraction = duration > 0f ? Mathf.Max(0f, (time - StartTime(frame)) / duration) : 0f;
            return frame;
        }

        internal ClipTiming Retarget(int frameRate, int minTickRate)
        {
            int per = TicksPerFrameOf(frameRate, minTickRate);
            int oldPer = TicksPerFrame;
            var frameTicks = new int[ticks.Length];
            for (int i = 0; i < ticks.Length; i++) frameTicks[i] = Mathf.Max(1, (ticks[i] * per * 2 + oldPer) / (oldPer * 2));
            return new ClipTiming(frameRate, TickRateOf(frameRate, minTickRate), frameTicks);
        }

        internal bool SameAs(ClipTiming other)
        {
            if (other == null || other.FrameCount != FrameCount) return false;
            if (Mathf.Abs(other.Length - Length) > CompareTolerance) return false;
            for (int i = 0; i < FrameCount; i++)
            {
                if (Mathf.Abs(other.StartTime(i) - StartTime(i)) > CompareTolerance) return false;
            }
            return true;
        }

        #endregion
    }
}
